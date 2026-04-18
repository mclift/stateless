param(
    [string]$Configuration = "Release",
    [string]$TargetFramework = "net8.0",
    [int]$Top = 12,
    [int]$AgentSliceSize = 8,
    [int]$AgentMaxIterations = 0,
    [int]$AgentMaxSignalsPerFile = 12,
    [switch]$SkipCodex,
    [ValidateSet("AnalyzeOnly", "ImplementTests")]
    [string]$CodexMode = "ImplementTests",
    [string]$CodexCommand = "codex.cmd",
    [string]$CodexModel = "",
    [ValidateSet("read-only", "workspace-write", "danger-full-access")]
    [string]$CodexSandbox = "workspace-write",
    [ValidateSet("never", "on-request", "on-failure", "untrusted")]
    [string]$CodexApproval = "never"
)

$ErrorActionPreference = "Stop"

function Get-RgLines {
    param(
        [string]$Pattern,
        [string[]]$Paths
    )

    if (-not (Get-Command rg -ErrorAction SilentlyContinue)) {
        throw "ripgrep (rg) is required to run the quality loop."
    }

    $output = & rg -n $Pattern @Paths -S 2>$null
    if ($LASTEXITCODE -eq 0) {
        return @($output)
    }

    if ($LASTEXITCODE -eq 1) {
        return @()
    }

    throw "ripgrep failed while searching for pattern: $Pattern"
}

function Get-CoverageSummary {
    param(
        [string]$CoverageJsonPath,
        [string]$RepoRoot
    )

    $coverage = Get-Content $CoverageJsonPath -Raw | ConvertFrom-Json
    $moduleProperty = $coverage.PSObject.Properties | Select-Object -First 1
    if (-not $moduleProperty) {
        throw "Coverage report '$CoverageJsonPath' did not contain any modules."
    }

    $rows = foreach ($fileProperty in $moduleProperty.Value.PSObject.Properties) {
        $linesCovered = 0
        $linesTotal = 0
        $branchesCovered = 0
        $branchesTotal = 0
        $methodsCovered = 0
        $methodsTotal = 0

        foreach ($classProperty in $fileProperty.Value.PSObject.Properties) {
            foreach ($methodProperty in $classProperty.Value.PSObject.Properties) {
                $methodsTotal++

                $lineHits = @($methodProperty.Value.Lines.PSObject.Properties | ForEach-Object { [int]$_.Value })
                if ($lineHits.Count -gt 0 -and ($lineHits | Measure-Object -Maximum).Maximum -gt 0) {
                    $methodsCovered++
                }

                foreach ($lineProperty in $methodProperty.Value.Lines.PSObject.Properties) {
                    $linesTotal++
                    if ([int]$lineProperty.Value -gt 0) {
                        $linesCovered++
                    }
                }

                foreach ($branch in $methodProperty.Value.Branches) {
                    $branchesTotal++
                    if ([int]$branch.Hits -gt 0) {
                        $branchesCovered++
                    }
                }
            }
        }

        [pscustomobject]@{
            File           = $fileProperty.Name.Replace("$RepoRoot\", "")
            LinePct        = if ($linesTotal) { [math]::Round(100 * $linesCovered / $linesTotal, 2) } else { 100 }
            BranchPct      = if ($branchesTotal) { [math]::Round(100 * $branchesCovered / $branchesTotal, 2) } else { 100 }
            MethodPct      = if ($methodsTotal) { [math]::Round(100 * $methodsCovered / $methodsTotal, 2) } else { 100 }
            LinesCovered   = $linesCovered
            LinesTotal     = $linesTotal
            BranchesCovered = $branchesCovered
            BranchesTotal   = $branchesTotal
            MethodsCovered = $methodsCovered
            MethodsTotal   = $methodsTotal
        }
    }

    $totals = [pscustomobject]@{
        LinePct   = if (($rows | Measure-Object LinesTotal -Sum).Sum) {
            [math]::Round(100 * (($rows | Measure-Object LinesCovered -Sum).Sum / ($rows | Measure-Object LinesTotal -Sum).Sum), 2)
        } else { 100 }
        BranchPct = if (($rows | Measure-Object BranchesTotal -Sum).Sum) {
            [math]::Round(100 * (($rows | Measure-Object BranchesCovered -Sum).Sum / ($rows | Measure-Object BranchesTotal -Sum).Sum), 2)
        } else { 100 }
        MethodPct = if (($rows | Measure-Object MethodsTotal -Sum).Sum) {
            [math]::Round(100 * (($rows | Measure-Object MethodsCovered -Sum).Sum / ($rows | Measure-Object MethodsTotal -Sum).Sum), 2)
        } else { 100 }
    }

    return [pscustomobject]@{
        Module = $moduleProperty.Name
        Totals = $totals
        Files  = @($rows | Sort-Object LinePct, BranchPct, MethodPct, File)
    }
}

function Get-TestCounters {
    param([string]$TrxPath)

    if (-not (Test-Path $TrxPath)) {
        return $null
    }

    [xml]$trx = Get-Content $TrxPath
    $counters = $trx.TestRun.ResultSummary.Counters

    return [pscustomobject]@{
        Total   = [int]$counters.total
        Passed  = [int]$counters.passed
        Failed  = [int]$counters.failed
        Skipped = [int]$counters.notExecuted
    }
}

function Get-RelativePath {
    param(
        [string]$Path,
        [string]$RepoRoot
    )

    $resolvedPath = (Resolve-Path -LiteralPath $Path).Path
    if ($resolvedPath.StartsWith("$RepoRoot\", [System.StringComparison]::OrdinalIgnoreCase)) {
        return $resolvedPath.Substring($RepoRoot.Length + 1)
    }

    return $resolvedPath
}

function Get-RelativeArtifactPath {
    param(
        [string]$Path,
        [string]$RepoRoot
    )

    if (Test-Path -LiteralPath $Path) {
        return Get-RelativePath -Path $Path -RepoRoot $RepoRoot
    }

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    if ($fullPath.StartsWith("$RepoRoot\", [System.StringComparison]::OrdinalIgnoreCase)) {
        return $fullPath.Substring($RepoRoot.Length + 1)
    }

    return $fullPath
}

function Get-LineMatches {
    param(
        [string[]]$Lines,
        [string]$Pattern
    )

    $found = @()
    for ($i = 0; $i -lt $Lines.Count; $i++) {
        if ($Lines[$i] -match $Pattern) {
            $found += [pscustomobject]@{
                Line = $i + 1
                Text = $Lines[$i].Trim()
            }
        }
    }

    return $found
}

function New-AgentFinding {
    param(
        [string]$Category,
        [string]$Severity,
        [string]$File,
        [int]$Line,
        [string]$Signal,
        [string]$Recommendation,
        [string]$RelatedTests = ""
    )

    return [pscustomobject]@{
        Category = $Category
        Severity = $Severity
        File = $File
        Line = $Line
        Signal = $Signal
        Recommendation = $Recommendation
        RelatedTests = $RelatedTests
    }
}

function Get-PublicMemberNames {
    param([string]$Content)

    $names = @()
    $matches = [regex]::Matches($Content, '(?m)^\s*public\s+(?:static\s+|async\s+|virtual\s+|override\s+|sealed\s+|partial\s+|abstract\s+)*[A-Za-z0-9_<>,\[\]\.?]+\s+([A-Z][A-Za-z0-9_]*)\s*\(')
    foreach ($match in $matches) {
        $names += $match.Groups[1].Value
    }

    return @($names | Sort-Object -Unique)
}

function Find-RelatedTests {
    param(
        [string]$SourceFile,
        [string[]]$PublicMemberNames,
        [object[]]$TestDocuments
    )

    $sourceBaseName = [System.IO.Path]::GetFileNameWithoutExtension($SourceFile)
    $tokens = @($sourceBaseName)

    if ($sourceBaseName.EndsWith(".Async", [System.StringComparison]::OrdinalIgnoreCase)) {
        $tokens += $sourceBaseName.Substring(0, $sourceBaseName.Length - 6)
    }

    $tokens += $PublicMemberNames
    $tokens = @($tokens | Where-Object { $_ } | Sort-Object -Unique)

    $related = @()
    foreach ($testDocument in $TestDocuments) {
        foreach ($token in $tokens) {
            if ($testDocument.Content -match [regex]::Escape($token)) {
                $related += $testDocument.RelativePath
                break
            }
        }
    }

    return @($related | Sort-Object -Unique)
}

function Format-RelatedTests {
    param([string[]]$RelatedTests)

    if (-not $RelatedTests -or $RelatedTests.Count -eq 0) {
        return ""
    }

    $shown = @($RelatedTests | Select-Object -First 3)
    if ($RelatedTests.Count -le $shown.Count) {
        return $shown -join ", "
    }

    return "$($shown -join ', '), ... (+$($RelatedTests.Count - $shown.Count) more)"
}

function Invoke-CodexAgentLoop {
    param(
        [string]$RepoRoot,
        [string]$ArtifactRoot,
        [string[]]$IterationFiles,
        [string]$Command,
        [string]$Mode,
        [string]$Model,
        [string]$Sandbox,
        [string]$Approval
    )

    $codex = Get-Command $Command -ErrorAction SilentlyContinue
    if (-not $codex) {
        throw "Codex command '$Command' was not found. Install Codex CLI or pass -SkipCodex for report-only mode."
    }

    $resultRoot = Join-Path $ArtifactRoot "agent-results"
    New-Item -ItemType Directory -Force -Path $resultRoot | Out-Null

    $results = @()
    $modeInstructions = if ($Mode -eq "AnalyzeOnly") {
        @"
Analyze only. Do not edit files. Read the scoped files, identify missing tests, edge cases, async invocation risks, security-adjacent issues, and any known upstream issue coverage gaps. Return a prioritized concise report with exact file paths and test ideas.
"@
    }
    else {
        @"
Implement safe missing tests when the expected behavior already passes. You may edit files under test/Stateless.Tests and docs only. Do not edit src/Stateless. If a test would expose a broken library behavior, do not add that failing test to the main suite; document the defect in docs/quality-findings.md instead. Run focused tests for changes you make and summarize the outcome.
"@
    }

    foreach ($iterationFile in $IterationFiles) {
        $iterationPath = Join-Path $RepoRoot $iterationFile
        $iterationName = [System.IO.Path]::GetFileNameWithoutExtension($iterationPath)
        $outputPath = Join-Path $resultRoot "$iterationName.codex.md"
        $logPath = Join-Path $resultRoot "$iterationName.codex.log"

        $prompt = @"
You are a Codex quality-improvement agent running inside the Stateless repository.

$modeInstructions

Important constraints:
- Review the iteration scope completely.
- Look specifically for missing test coverage, missing edge cases, asynchronous invocation problems, and security issues.
- Cross-check docs/quality-findings.md before documenting a new defect to avoid duplicates.
- Do not call Codex from inside this run.
- Do not run tools/Invoke-QualityLoop.ps1 from inside this run.
- Keep the final response concise and actionable.

Iteration prompt:

$(Get-Content -LiteralPath $iterationPath -Raw)
"@

        $arguments = @(
            "-a", $Approval,
            "exec",
            "-C", $RepoRoot,
            "--sandbox", $Sandbox,
            "--output-last-message", $outputPath,
            "--color", "never"
        )

        if ($Model) {
            $arguments += @("-m", $Model)
        }

        $arguments += "-"

        Write-Host "Invoking Codex for $iterationFile..."
        $previousErrorActionPreference = $ErrorActionPreference
        $ErrorActionPreference = "Continue"
        try {
            $prompt | & $codex.Source @arguments *> $logPath
            $exitCode = $LASTEXITCODE
        }
        finally {
            $ErrorActionPreference = $previousErrorActionPreference
        }

        $hasFinalMessage = (Test-Path -LiteralPath $outputPath) -and ((Get-Item -LiteralPath $outputPath).Length -gt 0)
        $status = if ($exitCode -eq 0) {
            "Completed"
        }
        elseif ($hasFinalMessage) {
            "CompletedWithWarnings"
        }
        else {
            "Failed"
        }

        $results += [pscustomobject]@{
            Iteration = $iterationFile
            ExitCode = $exitCode
            Status = $status
            Output = Get-RelativeArtifactPath -Path $outputPath -RepoRoot $RepoRoot
            Log = Get-RelativeArtifactPath -Path $logPath -RepoRoot $RepoRoot
        }

        if ($status -eq "Failed") {
            throw "Codex failed for '$iterationFile' with exit code $exitCode. See $logPath."
        }
    }

    $indexPath = Join-Path $resultRoot "index.md"
    $indexLines = @()
    $indexLines += "# Codex Agent Results"
    $indexLines += ""
    $indexLines += "- Mode: $Mode"
    $indexLines += "- Sandbox: $Sandbox"
    $indexLines += "- Approval: $Approval"
    $indexLines += "- Iterations invoked: $($results.Count)"
    $indexLines += ""
    $indexLines += "| Iteration | Status | Exit | Final Message | Log |"
    $indexLines += "| --- | --- | ---: | --- | --- |"
    foreach ($result in $results) {
        $indexLines += ('| `{0}` | {1} | {2} | `{3}` | `{4}` |' -f $result.Iteration, $result.Status, $result.ExitCode, $result.Output, $result.Log)
    }

    Set-Content -Path $indexPath -Value ($indexLines -join [Environment]::NewLine)

    return [pscustomobject]@{
        Invoked = $true
        Mode = $Mode
        Count = $results.Count
        Failed = @($results | Where-Object Status -eq "Failed").Count
        WarningCount = @($results | Where-Object Status -eq "CompletedWithWarnings").Count
        ResultIndex = Get-RelativePath -Path $indexPath -RepoRoot $RepoRoot
        Results = $results
    }
}

function Invoke-AgentReviewLoop {
    param(
        [string]$RepoRoot,
        [object]$CoverageSummary,
        [string]$ArtifactRoot,
        [int]$SliceSize,
        [int]$MaxIterations,
        [int]$MaxSignalsPerFile
    )

    if ($SliceSize -lt 1) {
        throw "AgentSliceSize must be at least 1."
    }

    $iterationRoot = Join-Path $ArtifactRoot "agent-iterations"
    New-Item -ItemType Directory -Force -Path $iterationRoot | Out-Null

    $sourceFiles = @(Get-ChildItem -Path (Join-Path $RepoRoot "src\Stateless") -Filter "*.cs" -Recurse |
        Where-Object { $_.FullName -notmatch '\\bin\\|\\obj\\' } |
        Sort-Object FullName)

    $testFiles = @(Get-ChildItem -Path (Join-Path $RepoRoot "test\Stateless.Tests") -Filter "*.cs" -Recurse |
        Where-Object { $_.FullName -notmatch '\\bin\\|\\obj\\|\\TestResults\\' } |
        Sort-Object FullName)

    $testDocuments = @($testFiles | ForEach-Object {
        [pscustomobject]@{
            FullName = $_.FullName
            RelativePath = Get-RelativePath -Path $_.FullName -RepoRoot $RepoRoot
            Content = Get-Content -LiteralPath $_.FullName -Raw
        }
    })

    $coverageByFile = @{}
    foreach ($row in $CoverageSummary.Files) {
        $coverageByFile[$row.File] = $row
    }

    $allFindings = @()
    $sourceInventory = @()

    foreach ($sourceFile in $sourceFiles) {
        $relativePath = Get-RelativePath -Path $sourceFile.FullName -RepoRoot $RepoRoot
        $content = Get-Content -LiteralPath $sourceFile.FullName -Raw
        $lines = @($content -split "\r?\n")
        $publicMembers = Get-PublicMemberNames -Content $content
        $relatedTests = Find-RelatedTests -SourceFile $relativePath -PublicMemberNames $publicMembers -TestDocuments $testDocuments
        $relatedTestsText = Format-RelatedTests -RelatedTests $relatedTests
        $coverage = $coverageByFile[$relativePath]
        $isGenerated = $relativePath -match '\.Designer\.cs$'

        $sourceInventory += [pscustomobject]@{
            Kind = "Source"
            File = $relativePath
            FullName = $sourceFile.FullName
            Lines = $lines.Count
            PublicMembers = $publicMembers
            RelatedTests = $relatedTests
            Coverage = $coverage
        }

        if ($coverage -and -not $isGenerated) {
            if ($coverage.LinePct -lt 50 -or $coverage.BranchPct -lt 40) {
                $allFindings += New-AgentFinding -Category "MissingTests" -Severity "High" -File $relativePath -Line 1 -Signal "Low coverage: line $($coverage.LinePct)%, branch $($coverage.BranchPct)%." -Recommendation "Inspect public behavior and add focused tests for uncovered branches before changing production code." -RelatedTests $relatedTestsText
            }
            elseif ($coverage.LinePct -lt 80 -or $coverage.BranchPct -lt 70) {
                $allFindings += New-AgentFinding -Category "MissingTests" -Severity "Medium" -File $relativePath -Line 1 -Signal "Coverage gap: line $($coverage.LinePct)%, branch $($coverage.BranchPct)%." -Recommendation "Look for missing success/failure/edge-case tests around uncovered methods and branches." -RelatedTests $relatedTestsText
            }
        }

        if ($publicMembers.Count -gt 0 -and $relatedTests.Count -eq 0 -and -not $isGenerated) {
            $allFindings += New-AgentFinding -Category "MissingTests" -Severity "Medium" -File $relativePath -Line 1 -Signal "No related test file appears to mention this source file or its public members." -Recommendation "Review whether this file has indirect coverage; add public behavior tests if not." -RelatedTests ""
        }

        $unmentionedMembers = @()
        foreach ($member in $publicMembers) {
            $mentioned = $false
            foreach ($testDocument in $testDocuments) {
                if ($testDocument.Content -match [regex]::Escape($member)) {
                    $mentioned = $true
                    break
                }
            }

            if (-not $mentioned) {
                $unmentionedMembers += $member
            }
        }

        if ($unmentionedMembers.Count -gt 0 -and -not $isGenerated) {
            $allFindings += New-AgentFinding -Category "MissingTests" -Severity "Low" -File $relativePath -Line 1 -Signal "Public members not directly mentioned in tests: $(@($unmentionedMembers | Select-Object -First 10) -join ', ')." -Recommendation "Confirm these members are covered indirectly or add targeted tests for their observable behavior." -RelatedTests $relatedTestsText
        }

        foreach ($match in @(Get-LineMatches -Lines $lines -Pattern '\.Result\b|\.Wait\(|GetAwaiter\(\)\.GetResult\(|ContinueWith\(|async void' | Select-Object -First $MaxSignalsPerFile)) {
            $severity = if ($match.Text -match 'ContinueWith|\.Result|\.Wait|GetAwaiter|async void') { "High" } else { "Medium" }
            $allFindings += New-AgentFinding -Category "Async" -Severity $severity -File $relativePath -Line $match.Line -Signal $match.Text -Recommendation "Check for sync-over-async, fire-and-forget work, exception loss, or test flakiness. Prefer a passing regression test if current behavior is intended." -RelatedTests $relatedTestsText
        }

        foreach ($match in @(Get-LineMatches -Lines $lines -Pattern 'Task\.Run\(|Task\.Factory\.StartNew\(|TaskScheduler\.FromCurrentSynchronizationContext\(|ConfigureAwait\(|SynchronizationContext|RetainSynchronizationContext' | Select-Object -First $MaxSignalsPerFile)) {
            $allFindings += New-AgentFinding -Category "Async" -Severity "Medium" -File $relativePath -Line $match.Line -Signal $match.Text -Recommendation "Verify callback ordering, context retention/loss, and completion semantics with async tests." -RelatedTests $relatedTestsText
        }

        foreach ($match in @(Get-LineMatches -Lines $lines -Pattern 'params object\[\]|ArgumentNullException|ArgumentException|args\.Length|default\(|default;|switch\s*\(|if\s*\(|throw new|Queue<|Dictionary<|ICollection<' | Select-Object -First $MaxSignalsPerFile)) {
            $allFindings += New-AgentFinding -Category "EdgeCases" -Severity "Low" -File $relativePath -Line $match.Line -Signal $match.Text -Recommendation "Check null, empty, boundary, ordering, duplicate, and invalid-argument behavior. Add passing tests or document current defects." -RelatedTests $relatedTestsText
        }

        foreach ($match in @(Get-LineMatches -Lines $lines -Pattern 'ToString\(\)|StringBuilder|Append(Line)?\(|FormatOne(Line|Transition|Cluster|State)|Escape|Saniti|Reflection|GetType\(|IsAssignableFrom|File\.|Path\.' | Select-Object -First $MaxSignalsPerFile)) {
            $severity = if ($relativePath -match 'Graph|Reflection|ParameterConversion|TriggerWithParameters') { "Medium" } else { "Low" }
            $allFindings += New-AgentFinding -Category "Security" -Severity $severity -File $relativePath -Line $match.Line -Signal $match.Text -Recommendation "Review escaping, identity collisions, reflection/type validation, path usage, and consumer-controlled output. Add tests for malicious or malformed values where behavior is safe." -RelatedTests $relatedTestsText
        }
    }

    $testInventory = @()
    foreach ($testFile in $testFiles) {
        $relativePath = Get-RelativePath -Path $testFile.FullName -RepoRoot $RepoRoot
        $content = Get-Content -LiteralPath $testFile.FullName -Raw
        $lines = @($content -split "\r?\n")
        $testInventory += [pscustomobject]@{
            Kind = "Test"
            File = $relativePath
            FullName = $testFile.FullName
            Lines = $lines.Count
        }

        foreach ($match in @(Get-LineMatches -Lines $lines -Pattern 'public\s+async\s+void|async\s+void' | Select-Object -First $MaxSignalsPerFile)) {
            $allFindings += New-AgentFinding -Category "TestQuality" -Severity "Medium" -File $relativePath -Line $match.Line -Signal $match.Text -Recommendation "Convert async void tests to async Task so failures are observed reliably."
        }

        foreach ($match in @(Get-LineMatches -Lines $lines -Pattern 'Task\.Run\(|Thread\.Sleep\(|Task\.Delay\(|\.Wait\(|\.Result\b' | Select-Object -First $MaxSignalsPerFile)) {
            $allFindings += New-AgentFinding -Category "TestQuality" -Severity "Low" -File $relativePath -Line $match.Line -Signal $match.Text -Recommendation "Review for timing-dependent or thread-pool-dependent tests; prefer deterministic TaskCompletionSource or direct Task.CompletedTask."
        }

        $factCount = ([regex]::Matches($content, '\[(Fact|Theory)\]')).Count
        $assertCount = ([regex]::Matches($content, 'Assert\.')).Count
        if ($factCount -gt 0 -and $assertCount -eq 0) {
            $allFindings += New-AgentFinding -Category "TestQuality" -Severity "Low" -File $relativePath -Line 1 -Signal "$factCount tests but no direct Assert usage." -Recommendation "Check whether the fixture only relies on no-throw behavior; add explicit assertions where helpful."
        }

        if ($lines.Count -gt 1000) {
            $allFindings += New-AgentFinding -Category "TestQuality" -Severity "Low" -File $relativePath -Line 1 -Signal "Large fixture: $($lines.Count) lines." -Recommendation "When adding coverage, consider focused fixture splits by behavior to keep future review easy."
        }
    }

    $severityRank = @{ High = 0; Medium = 1; Low = 2 }
    $findings = @($allFindings | Sort-Object @{ Expression = { $severityRank[$_.Severity] } }, Category, File, Line)
    $reviewInventory = @($sourceInventory + $testInventory | Sort-Object File)
    $iterationCount = [math]::Ceiling($reviewInventory.Count / $SliceSize)
    if ($MaxIterations -gt 0) {
        $iterationCount = [math]::Min($iterationCount, $MaxIterations)
    }

    $iterationFiles = @()
    for ($iteration = 0; $iteration -lt $iterationCount; $iteration++) {
        $start = $iteration * $SliceSize
        $slice = @($reviewInventory | Select-Object -Skip $start -First $SliceSize)
        $iterationNumber = $iteration + 1
        $iterationPath = Join-Path $iterationRoot ("iteration-{0:D3}.md" -f $iterationNumber)
        $sliceFiles = @($slice | ForEach-Object { $_.File })
        $sliceFindings = @($findings | Where-Object { $sliceFiles -contains $_.File })

        $lines = @()
        $lines += "# Agent Iteration $iterationNumber"
        $lines += ""
        $lines += "## Scope"
        $lines += ""
        foreach ($item in $slice) {
            $coverageText = ""
            if ($item.Kind -eq "Source" -and $item.Coverage) {
                $coverageText = " | line $($item.Coverage.LinePct)% | branch $($item.Coverage.BranchPct)%"
            }
            $lines += "- $($item.Kind): ``$($item.File)``$coverageText"
        }
        $lines += ""
        $lines += "## Mission"
        $lines += ""
        $lines += "1. Inspect every source and test file in scope."
        $lines += "2. Identify missing tests, edge cases, async invocation risks, and security-adjacent issues."
        $lines += "3. Implement passing tests for safe gaps."
        $lines += "4. Do not patch `src/Stateless` for a failing defect discovered by the new tests; record it in `docs/quality-findings.md`."
        $lines += "5. Rerun the focused tests, then the quality loop when the slice is complete."
        $lines += ""
        $lines += "## Detected Signals"
        $lines += ""

        if ($sliceFindings.Count -eq 0) {
            $lines += "- No heuristic findings for this slice. Still perform a manual read-through for untested behavior."
        }
        else {
            foreach ($finding in $sliceFindings) {
                $location = if ($finding.Line -gt 0) { "$($finding.File):$($finding.Line)" } else { $finding.File }
                $related = if ($finding.RelatedTests) { " Related tests: $($finding.RelatedTests)." } else { "" }
                $lines += "- [$($finding.Severity)] $($finding.Category) ``$location``: $($finding.Signal) Recommendation: $($finding.Recommendation)$related"
            }
        }

        $lines += ""
        $lines += "## Completion Criteria"
        $lines += ""
        $lines += "- Safe missing tests have been added."
        $lines += "- Defects that would make tests fail are documented instead of fixed."
        $lines += "- Any issue that looks security-sensitive has a test idea or documented finding."
        $lines += ""

        Set-Content -Path $iterationPath -Value ($lines -join [Environment]::NewLine)
        $iterationFiles += Get-RelativePath -Path $iterationPath -RepoRoot $RepoRoot
    }

    $backlogPath = Join-Path $ArtifactRoot "agent-backlog.md"
    $backlogLines = @()
    $backlogLines += "# Agent Backlog"
    $backlogLines += ""
    $backlogLines += "- Source files reviewed: $($sourceInventory.Count)"
    $backlogLines += "- Test files reviewed: $($testInventory.Count)"
    $backlogLines += "- Iterations generated: $iterationCount"
    $backlogLines += "- Findings generated: $($findings.Count)"
    $backlogLines += ""
    $backlogLines += "## Findings"
    $backlogLines += ""
    $backlogLines += "| Severity | Category | Location | Signal | Recommendation |"
    $backlogLines += "| --- | --- | --- | --- | --- |"
    foreach ($finding in $findings) {
        $location = if ($finding.Line -gt 0) { "$($finding.File):$($finding.Line)" } else { $finding.File }
        $signal = ($finding.Signal -replace '\|', '\|')
        $recommendation = ($finding.Recommendation -replace '\|', '\|')
        $backlogLines += "| $($finding.Severity) | $($finding.Category) | ``$location`` | $signal | $recommendation |"
    }

    Set-Content -Path $backlogPath -Value ($backlogLines -join [Environment]::NewLine)

    return [pscustomobject]@{
        SourceFileCount = $sourceInventory.Count
        TestFileCount = $testInventory.Count
        IterationCount = $iterationCount
        FindingCount = $findings.Count
        HighCount = @($findings | Where-Object Severity -eq "High").Count
        MediumCount = @($findings | Where-Object Severity -eq "Medium").Count
        LowCount = @($findings | Where-Object Severity -eq "Low").Count
        IterationFiles = $iterationFiles
        BacklogPath = Get-RelativePath -Path $backlogPath -RepoRoot $RepoRoot
        TopFindings = @($findings | Select-Object -First $Top)
    }
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$artifactRoot = Join-Path $repoRoot "artifacts\quality-loop"
$resultsRoot = Join-Path $artifactRoot "test-results"
$coverageBase = Join-Path $artifactRoot "coverage"
$coverageJsonPath = "$coverageBase.$TargetFramework.json"
$reportPath = Join-Path $artifactRoot "quality-loop-report.md"
$trxPath = Join-Path $resultsRoot "quality-loop.trx"

New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null
New-Item -ItemType Directory -Force -Path $resultsRoot | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $repoRoot ".dotnet") | Out-Null

$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_CLI_HOME = Join-Path $repoRoot ".dotnet"

Push-Location $repoRoot
try {
    & dotnet restore Stateless.sln -p:NuGetAudit=false -v minimal
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet restore failed."
    }

    & dotnet test test\Stateless.Tests\Stateless.Tests.csproj `
        --configuration $Configuration `
        --no-restore `
        --results-directory $resultsRoot `
        --logger "trx;LogFileName=quality-loop.trx" `
        -p:TargetFramework=$TargetFramework `
        -p:TargetFrameworks=$TargetFramework `
        -p:CollectCoverage=true `
        -p:CoverletOutputFormat=json `
        -p:CoverletOutput=$coverageBase `
        -v minimal
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet test failed."
    }
}
finally {
    Pop-Location
}

$coverageSummary = Get-CoverageSummary -CoverageJsonPath $coverageJsonPath -RepoRoot $repoRoot
$testCounters = Get-TestCounters -TrxPath $trxPath

$lowestCoverage = @($coverageSummary.Files | Select-Object -First $Top)

$asyncHotspots = Get-RgLines `
    -Pattern '\.Result\b|\.Wait\(|GetAwaiter\(\)\.GetResult\(|ContinueWith\(|Task\.Run\(|Task\.Factory\.StartNew\(|async void|TaskScheduler\.FromCurrentSynchronizationContext\(|SynchronizationContext' `
    -Paths @("src", "test")

$securityHotspots = Get-RgLines `
    -Pattern 'MermaidGraphStyle|FormatOneTransition|FormatOneLine|ParameterConversion|ValidateParameters|TriggerWithParameters|ToString\(\)' `
    -Paths @("src\Stateless", "test\Stateless.Tests")

$agentLoop = Invoke-AgentReviewLoop `
    -RepoRoot $repoRoot `
    -CoverageSummary $coverageSummary `
    -ArtifactRoot $artifactRoot `
    -SliceSize $AgentSliceSize `
    -MaxIterations $AgentMaxIterations `
    -MaxSignalsPerFile $AgentMaxSignalsPerFile

$codexLoop = if ($SkipCodex) {
    [pscustomobject]@{
        Invoked = $false
        Mode = $CodexMode
        Count = 0
        Failed = 0
        WarningCount = 0
        ResultIndex = ""
        Results = @()
    }
}
else {
    Invoke-CodexAgentLoop `
        -RepoRoot $repoRoot `
        -ArtifactRoot $artifactRoot `
        -IterationFiles $agentLoop.IterationFiles `
        -Command $CodexCommand `
        -Mode $CodexMode `
        -Model $CodexModel `
        -Sandbox $CodexSandbox `
        -Approval $CodexApproval
}

$tableLines = @(
    "| File | Line % | Branch % | Method % |"
    "| --- | ---: | ---: | ---: |"
)

foreach ($row in $lowestCoverage) {
    $tableLines += "| $($row.File) | $($row.LinePct) | $($row.BranchPct) | $($row.MethodPct) |"
}

$agentFindingLines = @(
    "| Severity | Category | Location | Recommendation |"
    "| --- | --- | --- | --- |"
)

foreach ($finding in $agentLoop.TopFindings) {
    $location = if ($finding.Line -gt 0) { "$($finding.File):$($finding.Line)" } else { $finding.File }
    $recommendation = ($finding.Recommendation -replace '\|', '\|')
    $agentFindingLines += "| $($finding.Severity) | $($finding.Category) | ``$location`` | $recommendation |"
}

if ($agentLoop.TopFindings.Count -eq 0) {
    $agentFindingLines += "| Low | ManualReview | repository | No heuristic findings were generated; perform a manual read-through anyway. |"
}

$codexResultLines = @(
    "| Iteration | Status | Exit | Final Message | Log |"
    "| --- | --- | ---: | --- | --- |"
)

if ($codexLoop.Invoked -and $codexLoop.Results.Count -gt 0) {
    foreach ($result in $codexLoop.Results) {
        $codexResultLines += ('| `{0}` | {1} | {2} | `{3}` | `{4}` |' -f $result.Iteration, $result.Status, $result.ExitCode, $result.Output, $result.Log)
    }
}
else {
    $codexResultLines += "| not invoked |  |  |  | Run without `-SkipCodex` to execute Codex agents. |"
}

$testSummaryLine = if ($testCounters) {
    "- Tests: $($testCounters.Passed) passed, $($testCounters.Failed) failed, $($testCounters.Skipped) skipped, $($testCounters.Total) total"
} else {
    "- Tests: see TRX report at artifacts/quality-loop/test-results/quality-loop.trx"
}

$reportLines = @()
$reportLines += "# Quality Loop Report"
$reportLines += ""
$reportLines += "- Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ssK')"
$reportLines += "- Target framework: $TargetFramework"
$reportLines += "- Configuration: $Configuration"
$reportLines += $testSummaryLine
$reportLines += "- Coverage: line $($coverageSummary.Totals.LinePct)% | branch $($coverageSummary.Totals.BranchPct)% | method $($coverageSummary.Totals.MethodPct)%"
$reportLines += ""
$reportLines += "## Lowest-Coverage Files"
$reportLines += ""
$reportLines += $tableLines
$reportLines += ""
$reportLines += "## Agent Loop"
$reportLines += ""
$reportLines += "- Source files reviewed: $($agentLoop.SourceFileCount)"
$reportLines += "- Test files reviewed: $($agentLoop.TestFileCount)"
$reportLines += "- Slice size: $AgentSliceSize"
$reportLines += "- Max signals per file: $AgentMaxSignalsPerFile"
$reportLines += "- Iterations generated: $($agentLoop.IterationCount)"
$reportLines += "- Findings generated: $($agentLoop.FindingCount) ($($agentLoop.HighCount) high, $($agentLoop.MediumCount) medium, $($agentLoop.LowCount) low)"
$reportLines += "- Backlog: ``$($agentLoop.BacklogPath)``"
$reportLines += ""
$reportLines += "Iteration files:"
foreach ($iterationFile in $agentLoop.IterationFiles) {
    $reportLines += "- ``$iterationFile``"
}
$reportLines += ""
$reportLines += "Top findings:"
$reportLines += ""
$reportLines += $agentFindingLines
$reportLines += ""
$reportLines += "## Codex Invocations"
$reportLines += ""
if ($codexLoop.Invoked) {
    $reportLines += "- Codex invoked: yes"
    $reportLines += "- Mode: $($codexLoop.Mode)"
    $reportLines += "- Iterations run: $($codexLoop.Count)"
    $reportLines += "- Completed with warnings: $($codexLoop.WarningCount)"
    $reportLines += "- Failed invocations: $($codexLoop.Failed)"
    $reportLines += "- Result index: ``$($codexLoop.ResultIndex)``"
}
else {
    $reportLines += "- Codex invoked: no (`-SkipCodex` was set)"
}
$reportLines += ""
$reportLines += $codexResultLines
$reportLines += ""
$reportLines += "## Async Hotspots"
$reportLines += ""
$reportLines += '```text'
$reportLines += if ($asyncHotspots.Count -gt 0) { $asyncHotspots } else { "(no matches)" }
$reportLines += '```'
$reportLines += ""
$reportLines += "## Security-Adjacent Hotspots"
$reportLines += ""
$reportLines += '```text'
$reportLines += if ($securityHotspots.Count -gt 0) { $securityHotspots } else { "(no matches)" }
$reportLines += '```'
$reportLines += ""
$reportLines += "## Agent Checklist"
$reportLines += ""
$reportLines += "1. Start with the lowest-coverage files and prefer tests that exercise public behavior over implementation details."
$reportLines += "2. Review async hotspots for sync-over-async, fire-and-forget continuations, callback ordering, and lost guard information."
$reportLines += "3. Review security-adjacent hotspots for output escaping, input validation, and identity collisions based on string formatting."
$reportLines += "4. Cross-check open and recent upstream GitHub issues for known behavior reports that need regression coverage."
$reportLines += "5. Add passing tests for genuine coverage gaps."
$reportLines += "6. If a new test exposes a library defect, do not patch the library in this loop; record the issue in docs/quality-findings.md instead."
$reportLines += ""

$report = $reportLines -join [Environment]::NewLine

Set-Content -Path $reportPath -Value $report

Write-Host "Quality loop completed."
Write-Host "Report: $reportPath"
Write-Host "Coverage JSON: $coverageJsonPath"
if ($codexLoop.Invoked) {
    Write-Host "Codex invocations: $($codexLoop.Count) run, $($codexLoop.WarningCount) completed with warnings, $($codexLoop.Failed) failed"
    Write-Host "Codex result index: $(Join-Path $repoRoot $codexLoop.ResultIndex)"
}
else {
    Write-Host "Codex invocations: skipped (-SkipCodex)"
}
