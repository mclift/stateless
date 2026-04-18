# Quality Findings

This file is for defects discovered by the quality loop that should be documented instead of fixed in the library during the same pass.

## Latest Verified Loop Pass

- Date: 2026-04-11
- Command: `powershell -ExecutionPolicy Bypass -File .\tools\Invoke-QualityLoop.ps1`
- Result: 435 passed, 0 failed, 0 skipped
- Coverage: 75.43% line, 65.42% branch, 82.28% method
- Agent loop: 60 source files and 31 test files reviewed across 12 generated iterations
- Agent backlog: `artifacts/quality-loop/agent-backlog.md`

## Intentional Failing Regression Coverage

Failing tests for currently documented defects live in `test/Stateless.Tests/QualityLoopKnownDefectFixture.cs` and are tagged with `QualityLoop=KnownDefect`.

Run only the known-defect tests:

```powershell
dotnet test .\test\Stateless.Tests\Stateless.Tests.csproj -c Release -f net8.0 --filter FullyQualifiedName~QualityLoopKnownDefectFixture
```

Run the regular green suite while excluding the intentional failures:

```powershell
dotnet test .\test\Stateless.Tests\Stateless.Tests.csproj -c Release -f net8.0 --filter "QualityLoop!=KnownDefect"
```

## Upstream Issue Cross-Check

Reviewed open and recent GitHub issues on 2026-04-11 for known behaviors that should drive coverage:

- `#645` Unmet guard descriptions are not propagated when a guard prevents a transition: matches the async unhandled-trigger defect below and is now represented by an intentional failing regression test.
- `#638` OnEntryAsync: covered by `FireAsync_DoesNotCompleteUntilAsyncEntryActionCompletes`, which pins that `FireAsync()` does not complete until async entry callbacks finish. Concurrent reads while the callback is still running remain outside the supported single-threaded usage model.
- `#619` OnTransitioned memory leak / unregister API: covered by `TransitionCallbackRegistrationFixture`, including sync unregister, async unregister, completed callbacks, and `UnregisterAllCallbacks()`.
- `#629` Substate transition hides superstate transition: already covered by `TransitionTests` and `AsyncTransitionTests` for sync and async fallback from a blocked substate guard to a superstate handler.
- `#604` PermitDynamic and reentry: already covered by dynamic transition fixtures that assert dynamic self-transitions invoke exit and entry behavior. This is mostly a documentation/design-semantics issue unless behavior changes.
- `#547` and `#587` DOT graph labels for internal transitions: covered by `DotGraphFixture.Internal_Transition_Does_Not_Show_Entry_Exit_Functions`.
- `#598` PermitIf guards with more than three parameters: currently a feature request for missing overloads rather than runtime behavior that can be covered without adding new API.

## Open Defects

### Async unhandled-trigger callbacks lose unmet guard descriptions

- File: `src/Stateless/StateMachine.Async.cs:217-220`
- Problem: `FireAsync()` passes `null` into `_unhandledTriggerAction.ExecuteAsync(...)` when a handler is rejected by guards, while the sync path forwards `UnmetGuardConditions`.
- Failing regression test: `OnUnhandledTriggerAsync_ReceivesUnmetGuardDescriptions_WhenGuardFails`

### Sync `Fire()` over `PermitDynamicAsync(...)` is fire-and-forget

- File: `src/Stateless/StateMachine.cs:422-431`
- Problem: the async destination selector is launched via `ContinueWith(...)` and not awaited. `Fire()` can return before state mutation completes and selector faults can be lost.
- Failing regression test: `Fire_WithPermitDynamicAsync_ThrowsInsteadOfFireAndForget`

### `FireAsync()` prefers closed sync handlers over open async handlers

- Files: `src/Stateless/StateRepresentation.Async.cs:213-234`
- Problem: async handler resolution computes sync and async candidates separately and then returns the sync result first. A closed sync handler can mask an open async handler, and cross-set ambiguity is never detected.
- Failing regression test: `FireAsync_PrefersOpenAsyncHandler_WhenSyncHandlerGuardsFail`

### Empty argument arrays can satisfy required trigger parameters with defaults

- Files: `src/Stateless/ParameterConversion.cs:9-35`, `src/Stateless/TriggerWithParameters.cs:35-41`
- Problem: `ParameterConversion.Unpack()` returns `null` or `default(T)` when `args.Length == 0`, so a required parameter can be treated as present when no arguments were supplied.
- Failing regression tests: `Fire_ParameterizedTriggerWithoutArguments_Throws`, `TriggerWithParameters_EmptyArgumentsForRequiredParameterThrowsArgumentException`

### Null arguments can satisfy non-nullable value-type trigger parameters

- Files: `src/Stateless/ParameterConversion.cs:20-23`, `src/Stateless/TriggerWithParameters.cs:42-46`
- Problem: `ParameterConversion.Unpack()` skips the assignability check when an argument value is `null`, so `TriggerWithParameters<int>.ValidateParameters(new object[] { null })` succeeds. Later packed callbacks can fail with `NullReferenceException` instead of a stable validation error.
- Failing regression test: `TriggerWithParameters_NullValueTypeArgumentThrowsArgumentException`

### Dynamic graph decision nodes can collide with user state names

- Files: `src/Stateless/Graph/Decision.cs:20-22`, `src/Stateless/Graph/StateGraph.cs:171-172`
- Problem: dynamic transition decision nodes are named `Decision{n}` without checking existing state node names. A user state named `Decision1` can share the same graph node id as the first dynamic transition decision node, producing ambiguous DOT/Mermaid output and potentially misrepresenting transitions.
- Failing regression test: `Graph_DynamicDecisionNodeName_DoesNotCollideWithStateName`

### GraphStyleBase does not tolerate null underlying triggers

- File: `src/Stateless/Graph/GraphStyleBase.cs:80-99`
- Problem: `FormatAllTransitions()` calls `transit.Trigger.UnderlyingTrigger.ToString()` for stay, fixed, and dynamic transitions. A `TriggerInfo` wrapping a null trigger, which otherwise formats as `SpecialConstants.NullString`, throws `NullReferenceException` before a graph style can render the transition.
- Failing regression test: `FormatAllTransitions_UsesTriggerInfoToStringForNullUnderlyingTrigger`

### Mermaid labels are emitted without escaping

- Files: `src/Stateless/Graph/MermaidGraphStyle.cs:80-108`, `src/Stateless/Graph/MermaidGraphStyle.cs:124-155`
- Problem: state names, trigger names, action names, and guard descriptions are interpolated directly into Mermaid output. Consumer-controlled values can break diagram syntax or inject Mermaid directives.
- Failing regression test: `MermaidGraph_EscapesLabelsAndGuardDescriptions`

### Graph construction collapses distinct states with identical string representations

- Files: `src/Stateless/Graph/StateGraph.cs:213-238`, `src/Stateless/Graph/State.cs:53-54`
- Problem: graph nodes are keyed and named with `UnderlyingState.ToString()`. Two distinct state values that compare unequal but return the same string can share one graph entry, causing DOT/Mermaid output to omit a state or render a transition against the wrong node.
- Failing regression test: `Graph_DistinctStatesWithSameStringRepresentation_DoNotCollapse`

### Reflection info omits async fixed trigger behaviours

- Files: `src/Stateless/StateMachine.cs:165-167`, `src/Stateless/Reflection/StateInfo.cs:44-66`, `src/Stateless/ReentryTriggerBehaviour.async.cs`
- Problem: `GetInfo()` only discovers relationships from `StateRepresentation.TriggerBehaviours`. Fixed async transitions configured through `PermitIfAsync(...)` and `PermitReentryIfAsync(...)` are stored in `TriggerBehavioursAsync`, so reflection metadata and graph output omit those transitions.
- Failing regression test: `GetInfo_ReflectsPermitReentryIfAsyncAsFixedTransition`

### Activation is documented as idempotent but callbacks re-execute

- Files: `src/Stateless/StateMachine.cs:308-314`, `src/Stateless/StateMachine.Async.cs:12-18`, `src/Stateless/StateRepresentation.cs:145-165`, `src/Stateless/StateRepresentation.Async.cs:47-67`
- Problem: `Activate()` and `ActivateAsync()` documentation says repeated activation is idempotent, but the state representation does not track activation state and re-runs activation callbacks on repeated calls.
- Failing regression tests: `Activate_CalledTwice_DoesNotReexecuteOnActivateCallbacks`, `ActivateAsync_CalledTwice_DoesNotReexecuteOnActivateCallbacks`

### Some action configuration overloads accept null callbacks until invocation

- Files: `src/Stateless/StateConfiguration.cs:826-849`, `src/Stateless/StateConfiguration.cs:1108-1115`, `src/Stateless/StateConfiguration.Async.cs:258-281`, `src/Stateless/StateConfiguration.Async.cs:517-524`
- Problem: `OnActivate(...)`, `OnDeactivate(...)`, `OnExit(Action<Transition>)`, `OnActivateAsync(...)`, `OnDeactivateAsync(...)`, and `OnExitAsync(Func<Transition, Task>)` do not reject null callbacks at configuration time. The null delegate is stored in the action behaviour and later invocation fails with `NullReferenceException` instead of a stable `ArgumentNullException` from the public API boundary.
- Failing regression test: `StateConfiguration_NullActionCallbacksThrowArgumentNullExceptionAtConfiguration`

### Some guard configuration overloads accept null guards until invocation

- Files: `src/Stateless/GuardCondition.cs:18-21`, `src/Stateless/GuardConditionAsync.cs:19-22`, `src/Stateless/StateConfiguration.cs:295-302`, `src/Stateless/StateConfiguration.Async.cs:1075-1082`
- Problem: no-argument sync and async guard constructors wrap the supplied guard before null validation, so public overloads such as `PermitIf(..., Func<bool> guard)` and `PermitIfAsync(..., Func<Task<bool>> guard)` can accept `null` at configuration time. Firing the trigger later invokes the stored wrapper and fails with `NullReferenceException` instead of a stable `ArgumentNullException` at the public API boundary.
- Failing regression test: `StateConfiguration_NullGuardCallbacksThrowArgumentNullExceptionAtConfiguration`

## Review Notes

- The README already documents that the state machine is single-threaded and should not be used concurrently from multiple threads.
- No direct credential handling, cryptography, network transport, or file-write security issues were found in this pass.
