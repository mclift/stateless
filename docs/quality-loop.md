# Quality Loop

Use the loop runner to keep quality work repeatable and narrow:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\Invoke-QualityLoop.ps1
```

The loop does five things in one pass:

1. Restores the solution with a repo-local `.dotnet` home.
2. Runs the `Stateless.Tests` suite against a single stable target framework.
3. Collects JSON coverage with `coverlet.msbuild`.
4. Iterates across every source and test file in review slices.
5. Scores missing-test, edge-case, async, security-adjacent, and test-quality signals.
6. Emits a markdown report under `artifacts/quality-loop/quality-loop-report.md`.
7. Writes an agent backlog and per-iteration prompt files under `artifacts/quality-loop/`.

The generated agent loop outputs are:

- `artifacts/quality-loop/agent-backlog.md`: consolidated prioritized findings across the whole codebase and tests.
- `artifacts/quality-loop/agent-iterations/iteration-*.md`: one scoped prompt per slice of files, with mission, detected signals, and completion criteria.
- `artifacts/quality-loop/agent-results/iteration-*.codex.md`: Codex's final response for each iteration.
- `artifacts/quality-loop/agent-results/iteration-*.codex.log`: full Codex CLI log for each iteration.
- `artifacts/quality-loop/agent-results/index.md`: index of Codex invocations and outputs.
- `artifacts/quality-loop/quality-loop-report.md`: test, coverage, and agent-loop summary.

By default, `Invoke-QualityLoop.ps1` invokes Codex with `codex.cmd exec` for each generated iteration. Use `-SkipCodex` only when you want a fast report-only run that does not call Codex.

The Codex result index records each invocation as `Completed`, `CompletedWithWarnings`, or `Failed`. `CompletedWithWarnings` means Codex returned a final analysis message, but the local CLI also wrote a nonzero exit or warning that should be reviewed in the matching `.codex.log`.

Codex execution modes:

- `-CodexMode ImplementTests`: default. Codex may add safe passing tests and update docs, but must not edit `src/Stateless`.
- `-CodexMode AnalyzeOnly`: Codex analyzes each iteration and writes recommendations without editing files.

## Why The Loop Pins `net8.0`

The full project graph multi-targets frameworks that are not equally runnable on every local machine. The loop defaults to `net8.0` so coverage and test baselines stay consistent for agent-driven review work.

If your environment supports a different slice, override it:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\Invoke-QualityLoop.ps1 -TargetFramework net9.0
```

Tune the review-loop slice size if you want smaller or larger agent work packets:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\Invoke-QualityLoop.ps1 -AgentSliceSize 6
```

By default, the loop covers the entire source and test tree. Use `-AgentMaxIterations` only when you intentionally want a partial dry run. Use `-AgentMaxSignalsPerFile` if you want more or fewer heuristic signals per file in the generated prompts.

For a quick smoke test that proves Codex invocation works without editing files:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\Invoke-QualityLoop.ps1 -AgentMaxIterations 1 -CodexMode AnalyzeOnly -CodexSandbox read-only
```

## Operating Rules

1. Start with the lowest-coverage files in the generated report.
2. Prefer tests that cover public behavior and real use cases.
3. Always review async hotspots for fire-and-forget work, sync-over-async, callback ordering, and lost guard information.
4. Always review security-adjacent hotspots for escaping, input validation, and identity collisions.
5. Cross-check open and recent upstream GitHub issues for behavior reports that need regression coverage.
6. Add passing tests for safe coverage gaps.
7. If a new test reveals a library defect, do not fix the library as part of this loop. Record the defect in `docs/quality-findings.md` and keep the main suite green.
