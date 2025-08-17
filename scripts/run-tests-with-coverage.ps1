#!/usr/bin/env pwsh

# Run tests with coverage collection
dotnet test --no-restore --no-build --configuration Release --collect:"XPlat Code Coverage" --results-directory ./coverage --logger "console;verbosity=normal"

# Get test results summary from the output
$testOutput = dotnet test --no-restore --no-build --configuration Release --logger "console;verbosity=minimal" 2>&1
$testSummary = $testOutput | Select-String "Test Run" | Select-Object -Last 1

# If no test summary found, create a default one
if (-not $testSummary) {
  $testSummary = "Test execution completed"
}

# Get build configuration from environment or default
$buildConfig = if ($env:BUILD_CONFIGURATION) { $env:BUILD_CONFIGURATION } else { "Release" }

# Get target frameworks from test project file
$testProjectPath = "./test/Stateless.Tests/Stateless.Tests.csproj"
$targetFrameworks = "Unknown"
if (Test-Path $testProjectPath) {
  try {
    [xml]$testProject = Get-Content $testProjectPath
    $targetFrameworks = $testProject.Project.PropertyGroup.TargetFrameworks
    if (-not $targetFrameworks) {
      $targetFrameworks = $testProject.Project.PropertyGroup.TargetFramework
    }
    if (-not $targetFrameworks) {
      $targetFrameworks = "Unknown"
    }
  } catch {
    $targetFrameworks = "Error reading project file"
  }
}

# Get coverage percentage
$coveragePercentage = "N/A"
$badgeColor = "lightgrey"

try {
  $coverageFile = Get-ChildItem -Path "./coverage" -Recurse -Filter "coverage.cobertura.xml" | Select-Object -First 1
  if ($coverageFile) {
    [xml]$coverage = Get-Content $coverageFile.FullName
    $lineRate = [double]$coverage.coverage.'line-rate'
    $coveragePercentage = [math]::Round($lineRate * 100, 1)
    
    $badgeColor = if ($coveragePercentage -ge 90) { "brightgreen" } elseif ($coveragePercentage -ge 80) { "green" } elseif ($coveragePercentage -ge 70) { "yellow" } elseif ($coveragePercentage -ge 60) { "orange" } else { "red" }
  }
} catch {
  $coveragePercentage = "Error calculating coverage"
  $badgeColor = "lightgrey"
}

  # Create summary
  $summary = "## Test Results`n`n"
  $summary += "**Tests:** $testSummary`n"
  $summary += "**Coverage:** $coveragePercentage%`n"
  $summary += "**Target Frameworks:** $targetFrameworks`n"
  $summary += "**Configuration:** $buildConfig`n`n"

if ($coveragePercentage -ne "N/A" -and $coveragePercentage -ne "Error calculating coverage") {
  $summary += "![Coverage](https://img.shields.io/badge/coverage-${coveragePercentage}%25-${badgeColor}?style=flat-square)"
}

echo "summary<<EOF" >> $env:GITHUB_OUTPUT
echo "$summary" >> $env:GITHUB_OUTPUT
echo "EOF" >> $env:GITHUB_OUTPUT
echo "coverage_percentage=$coveragePercentage" >> $env:GITHUB_OUTPUT
echo "badge_color=$badgeColor" >> $env:GITHUB_OUTPUT
