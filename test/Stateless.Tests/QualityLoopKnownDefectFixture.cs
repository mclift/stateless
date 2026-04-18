using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Stateless.Reflection;
using Xunit;
using GraphFixedTransition = Stateless.Graph.FixedTransition;
using GraphState = Stateless.Graph.State;
using GraphStyleBase = Stateless.Graph.GraphStyleBase;

namespace Stateless.Tests
{
    public class QualityLoopKnownDefectFixture
    {
        [Fact]
        [Trait("QualityLoop", "KnownDefect")]
        public async Task OnUnhandledTriggerAsync_ReceivesUnmetGuardDescriptions_WhenGuardFails()
        {
            var sm = new StateMachine<State, Trigger>(State.A);
            ICollection<string> receivedUnmetGuards = null;

            sm.Configure(State.A)
                .PermitIf(Trigger.X, State.B, () => false, "Guard must pass");

            sm.OnUnhandledTriggerAsync((state, trigger, unmetGuards) =>
            {
                receivedUnmetGuards = unmetGuards;
                return Task.CompletedTask;
            });

            await sm.FireAsync(Trigger.X).ConfigureAwait(false);

            Assert.NotNull(receivedUnmetGuards);
            Assert.Contains("Guard must pass", receivedUnmetGuards);
        }

        [Fact]
        [Trait("QualityLoop", "KnownDefect")]
        public void Fire_WithPermitDynamicAsync_ThrowsInsteadOfFireAndForget()
        {
            var sm = new StateMachine<State, Trigger>(State.A);

            sm.Configure(State.A)
                .PermitDynamicAsync(Trigger.X, () => Task.FromResult(State.B));

            Assert.Throws<InvalidOperationException>(() => sm.Fire(Trigger.X));
        }

        [Fact]
        [Trait("QualityLoop", "KnownDefect")]
        public async Task FireAsync_PrefersOpenAsyncHandler_WhenSyncHandlerGuardsFail()
        {
            var sm = new StateMachine<State, Trigger>(State.A);

            sm.Configure(State.A)
                .PermitIf(Trigger.X, State.B, () => false, "sync path closed")
                .PermitIfAsync(Trigger.X, State.C, () => Task.FromResult(true), "async path open");

            await sm.FireAsync(Trigger.X).ConfigureAwait(false);

            Assert.Equal(State.C, sm.State);
        }

        [Fact]
        [Trait("QualityLoop", "KnownDefect")]
        public void Fire_ParameterizedTriggerWithoutArguments_Throws()
        {
            var sm = new StateMachine<State, Trigger>(State.A);
            var trigger = sm.SetTriggerParameters<int>(Trigger.X);

            sm.Configure(State.A)
                .PermitIf(trigger, State.B, _ => true);

            Assert.Throws<ArgumentException>(() => sm.Fire(trigger));
        }

        [Fact]
        [Trait("QualityLoop", "KnownDefect")]
        public void TriggerWithParameters_EmptyArgumentsForRequiredParameterThrowsArgumentException()
        {
            var trigger = new StateMachine<State, Trigger>.TriggerWithParameters<int>(Trigger.X);

            Assert.Throws<ArgumentException>(() => trigger.ValidateParameters(Array.Empty<object>()));
        }

        [Fact]
        [Trait("QualityLoop", "KnownDefect")]
        public void TriggerWithParameters_NullValueTypeArgumentThrowsArgumentException()
        {
            var trigger = new StateMachine<State, Trigger>.TriggerWithParameters<int>(Trigger.X);

            Assert.Throws<ArgumentException>(() => trigger.ValidateParameters(new object[] { null }));
        }

        [Fact]
        [Trait("QualityLoop", "KnownDefect")]
        public void Graph_DynamicDecisionNodeName_DoesNotCollideWithStateName()
        {
            var sm = new StateMachine<string, string>("A");

            sm.Configure("A")
                .PermitDynamic("Choose", () => "B");
            sm.Configure("B");
            sm.Configure("Decision1");

            var graph = Graph.UmlDotGraph.Format(sm.GetInfo());

            Assert.Contains("\"Decision2\" [shape = \"diamond\"", graph);
            Assert.DoesNotContain("\"Decision1\" [shape = \"diamond\"", graph);
        }

        [Fact]
        [Trait("QualityLoop", "KnownDefect")]
        public void FormatAllTransitions_UsesTriggerInfoToStringForNullUnderlyingTrigger()
        {
            var style = new RecordingGraphStyle();
            var transition = new GraphFixedTransition(
                new GraphState("Source"),
                new GraphState("Destination"),
                new TriggerInfo(null),
                Array.Empty<InvocationInfo>());

            var lines = style.FormatAllTransitions(new List<Stateless.Graph.Transition> { transition });

            Assert.Single(lines);
            var call = Assert.Single(style.Calls);
            Assert.Equal(SpecialConstants.NullString, call.Trigger);
        }

        [Fact]
        [Trait("QualityLoop", "KnownDefect")]
        public void MermaidGraph_EscapesLabelsAndGuardDescriptions()
        {
            var unsafeState = "B\n%% injected state";
            var unsafeTrigger = "X\n%% injected trigger";
            var unsafeGuardDescription = "guard\n%% injected guard";
            var sm = new StateMachine<string, string>("A");

            sm.Configure("A")
                .PermitIf(unsafeTrigger, unsafeState, () => true, unsafeGuardDescription);
            sm.Configure(unsafeState);

            var graph = Graph.MermaidGraph.Format(sm.GetInfo());

            Assert.DoesNotContain("\n%% injected state", graph);
            Assert.DoesNotContain("\n%% injected trigger", graph);
            Assert.DoesNotContain("\n%% injected guard", graph);
        }

        [Fact]
        [Trait("QualityLoop", "KnownDefect")]
        public void Graph_DistinctStatesWithSameStringRepresentation_DoNotCollapse()
        {
            var first = new DuplicateLabelState();
            var second = new DuplicateLabelState();
            var sm = new StateMachine<DuplicateLabelState, string>(first);

            sm.Configure(first)
                .Permit("Go", second);
            sm.Configure(second);

            var graph = new Stateless.Graph.StateGraph(sm.GetInfo());

            Assert.Equal(2, graph.States.Count);
        }

        [Fact]
        [Trait("QualityLoop", "KnownDefect")]
        public void GetInfo_ReflectsPermitReentryIfAsyncAsFixedTransition()
        {
            var sm = new StateMachine<State, Trigger>(State.A);

            sm.Configure(State.A)
                .PermitReentryIfAsync(Trigger.X, () => Task.FromResult(true));

            var stateInfo = Assert.Single(sm.GetInfo().States.Where(state => Equals(state.UnderlyingState, State.A)));
            var transition = Assert.Single(stateInfo.FixedTransitions);

            Assert.Equal(Trigger.X.ToString(), transition.Trigger.ToString());
            Assert.Equal(State.A, transition.DestinationState.UnderlyingState);
        }

        [Fact]
        [Trait("QualityLoop", "KnownDefect")]
        public void Activate_CalledTwice_DoesNotReexecuteOnActivateCallbacks()
        {
            var sm = new StateMachine<State, Trigger>(State.A);
            var activationCount = 0;

            sm.Configure(State.A)
                .OnActivate(() => activationCount++);

            sm.Activate();
            sm.Activate();

            Assert.Equal(1, activationCount);
        }

        [Fact]
        [Trait("QualityLoop", "KnownDefect")]
        public async Task ActivateAsync_CalledTwice_DoesNotReexecuteOnActivateCallbacks()
        {
            var sm = new StateMachine<State, Trigger>(State.A);
            var activationCount = 0;

            sm.Configure(State.A)
                .OnActivateAsync(() =>
                {
                    activationCount++;
                    return Task.CompletedTask;
                });

            await sm.ActivateAsync().ConfigureAwait(false);
            await sm.ActivateAsync().ConfigureAwait(false);

            Assert.Equal(1, activationCount);
        }

        [Fact]
        [Trait("QualityLoop", "KnownDefect")]
        public void StateConfiguration_NullActionCallbacksThrowArgumentNullExceptionAtConfiguration()
        {
            var sm = new StateMachine<State, Trigger>(State.A);

            Assert.Throws<ArgumentNullException>(() => sm.Configure(State.A).OnActivate((Action)null));
            Assert.Throws<ArgumentNullException>(() => sm.Configure(State.A).OnDeactivate((Action)null));
            Assert.Throws<ArgumentNullException>(() => sm.Configure(State.A).OnExit((Action<StateMachine<State, Trigger>.Transition>)null));
            Assert.Throws<ArgumentNullException>(() => sm.Configure(State.A).OnActivateAsync((Func<Task>)null));
            Assert.Throws<ArgumentNullException>(() => sm.Configure(State.A).OnDeactivateAsync((Func<Task>)null));
            Assert.Throws<ArgumentNullException>(() => sm.Configure(State.A).OnExitAsync((Func<StateMachine<State, Trigger>.Transition, Task>)null));
        }

        [Fact]
        [Trait("QualityLoop", "KnownDefect")]
        public void StateConfiguration_NullGuardCallbacksThrowArgumentNullExceptionAtConfiguration()
        {
            var sm = new StateMachine<State, Trigger>(State.A);

            Assert.Throws<ArgumentNullException>(() => sm.Configure(State.A).PermitIf(Trigger.X, State.B, (Func<bool>)null));
            Assert.Throws<ArgumentNullException>(() => sm.Configure(State.A).PermitIfAsync(Trigger.Y, State.C, (Func<Task<bool>>)null));
        }

        [Fact]
        [Trait("QualityLoop", "KnownDefect")]
        public async Task GetPermittedTriggersAsync_WithMixedParameterizedTriggers_IgnoresIncompatibleTriggerArguments()
        {
            var sm = new StateMachine<State, Trigger>(State.A);
            var sendToReview = sm.SetTriggerParameters<string>(Trigger.X);
            var sendToWork = sm.SetTriggerParameters<Guid>(Trigger.Y);
            var documentId = Guid.NewGuid();

            sm.Configure(State.A)
                .PermitIf(sendToReview, State.B, reason => !string.IsNullOrWhiteSpace(reason))
                .PermitIf(sendToWork, State.C, id => id == documentId);

            var permittedTriggers = await sm.GetPermittedTriggersAsync(documentId).ConfigureAwait(false);

            Assert.DoesNotContain(Trigger.X, permittedTriggers);
            Assert.Contains(Trigger.Y, permittedTriggers);
        }

        private sealed class RecordingGraphStyle : GraphStyleBase
        {
            public readonly List<TransitionCall> Calls = new List<TransitionCall>();

            public override string GetPrefix()
            {
                return string.Empty;
            }

            public override string GetInitialTransition(StateInfo initialState)
            {
                return string.Empty;
            }

            public override string FormatOneState(GraphState state)
            {
                return string.Empty;
            }

            public override string FormatOneCluster(Stateless.Graph.SuperState stateInfo)
            {
                return string.Empty;
            }

            public override string FormatOneDecisionNode(string nodeName, string label)
            {
                return string.Empty;
            }

            public override string FormatOneTransition(
                string sourceNodeName,
                string trigger,
                IEnumerable<string> actions,
                string destinationNodeName,
                IEnumerable<string> guards)
            {
                Calls.Add(new TransitionCall
                {
                    SourceNodeName = sourceNodeName,
                    Trigger = trigger,
                    DestinationNodeName = destinationNodeName
                });

                return $"{sourceNodeName}->{destinationNodeName}:{trigger}";
            }
        }

        private sealed class TransitionCall
        {
            public string SourceNodeName { get; set; }

            public string Trigger { get; set; }

            public string DestinationNodeName { get; set; }
        }

        private sealed class DuplicateLabelState
        {
            public override string ToString()
            {
                return "Duplicate";
            }
        }
    }
}
