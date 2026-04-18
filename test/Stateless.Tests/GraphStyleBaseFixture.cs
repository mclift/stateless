using System;
using System.Collections.Generic;
using System.Linq;
using Stateless.Reflection;
using Xunit;
using GraphFixedTransition = Stateless.Graph.FixedTransition;
using GraphState = Stateless.Graph.State;
using GraphStyleBase = Stateless.Graph.GraphStyleBase;
using GraphTransition = Stateless.Graph.Transition;
using GraphDynamicTransition = Stateless.Graph.DynamicTransition;
using GraphStayTransition = Stateless.Graph.StayTransition;

namespace Stateless.Tests
{
    public class GraphStyleBaseFixture
    {
        [Fact]
        public void FormatAllTransitionsReturnsEmptyListForNullTransitions()
        {
            var style = new RecordingGraphStyle();

            var lines = style.FormatAllTransitions(null);

            Assert.Empty(lines);
            Assert.Empty(style.Calls);
        }

        [Fact]
        public void FormatAllTransitionsPassesExpectedValuesForTransitionTypes()
        {
            var source = new GraphState("Source");
            var destination = new GraphState("Destination");
            var trigger = new TriggerInfo("Go");
            var style = new RecordingGraphStyle();

            var internalStay = new GraphStayTransition(source, trigger, new[] { Invocation("GuardA") }, false);

            var reentryStay = new GraphStayTransition(source, trigger, new[] { Invocation("GuardB") }, true);
            reentryStay.DestinationEntryActions.Add(Action("EntryA"));

            var fixedTransition = new GraphFixedTransition(source, destination, trigger, new[] { Invocation("GuardC") });
            fixedTransition.DestinationEntryActions.Add(Action("EntryB"));

            var dynamicTransition = new GraphDynamicTransition(source, destination, trigger, "ChoseDestination");
            dynamicTransition.DestinationEntryActions.Add(Action("EntryC"));

            var lines = style.FormatAllTransitions(new List<GraphTransition>
            {
                internalStay,
                reentryStay,
                fixedTransition,
                dynamicTransition
            });

            Assert.Equal(new[]
            {
                "Source->Source:Go",
                "Source->Source:Go",
                "Source->Destination:Go",
                "Source->Destination:Go"
            }, lines);

            Assert.Collection(style.Calls,
                call =>
                {
                    Assert.Equal("Source", call.SourceNodeName);
                    Assert.Equal("Go", call.Trigger);
                    Assert.Null(call.Actions);
                    Assert.Equal("Source", call.DestinationNodeName);
                    Assert.Equal(new[] { "GuardA" }, call.Guards);
                },
                call =>
                {
                    Assert.Equal("Source", call.SourceNodeName);
                    Assert.Equal("Go", call.Trigger);
                    Assert.Equal(new[] { "EntryA" }, call.Actions);
                    Assert.Equal("Source", call.DestinationNodeName);
                    Assert.Equal(new[] { "GuardB" }, call.Guards);
                },
                call =>
                {
                    Assert.Equal("Source", call.SourceNodeName);
                    Assert.Equal("Go", call.Trigger);
                    Assert.Equal(new[] { "EntryB" }, call.Actions);
                    Assert.Equal("Destination", call.DestinationNodeName);
                    Assert.Equal(new[] { "GuardC" }, call.Guards);
                },
                call =>
                {
                    Assert.Equal("Source", call.SourceNodeName);
                    Assert.Equal("Go", call.Trigger);
                    Assert.Equal(new[] { "EntryC" }, call.Actions);
                    Assert.Equal("Destination", call.DestinationNodeName);
                    Assert.Equal(new[] { "ChoseDestination" }, call.Guards);
                });
        }

        [Fact]
        public void FormatAllTransitionsPassesRawConsumerControlledValuesToStyle()
        {
            var sourceName = "\\source \"A\"\n[raw]";
            var destinationName = "\\destination \"B\"\n[raw]";
            var trigger = new LabelledValue("\\trigger \"Go\"\n[raw]");
            var entryAction = "\\entry \"Run\"\n[raw]";
            var guard = "\\guard \"Allowed\"\n[raw]";
            var transition = new GraphFixedTransition(
                new GraphState(sourceName),
                new GraphState(destinationName),
                new TriggerInfo(trigger),
                new[] { Invocation(guard) });
            transition.DestinationEntryActions.Add(Action(entryAction));
            var style = new RecordingGraphStyle();

            var lines = style.FormatAllTransitions(new List<GraphTransition> { transition });

            Assert.Single(lines);
            var call = Assert.Single(style.Calls);
            Assert.Equal(sourceName, call.SourceNodeName);
            Assert.Equal(trigger.ToString(), call.Trigger);
            Assert.Equal(new[] { entryAction }, call.Actions);
            Assert.Equal(destinationName, call.DestinationNodeName);
            Assert.Equal(new[] { guard }, call.Guards);
        }

        [Fact]
        public void FormatAllTransitionsSkipsNullFormattedLines()
        {
            var style = new RecordingGraphStyle { ReturnNull = true };
            var transition = new GraphFixedTransition(
                new GraphState("Source"),
                new GraphState("Destination"),
                new TriggerInfo("Go"),
                new InvocationInfo[0]);

            var lines = style.FormatAllTransitions(new List<GraphTransition> { transition });

            Assert.Empty(lines);
            Assert.Single(style.Calls);
        }

        [Fact]
        public void FormatAllTransitionsThrowsForUnexpectedTransitionType()
        {
            var style = new RecordingGraphStyle();
            var transition = new UnknownTransition(new GraphState("Source"), new TriggerInfo("Go"));

            Assert.Throws<ArgumentException>(() => style.FormatAllTransitions(new List<GraphTransition> { transition }));
        }

        [Fact]
        public void FormatAllTransitionsRequiresFormatOneTransitionOverride()
        {
            var style = new DefaultTransitionStyle();
            var transition = new GraphFixedTransition(
                new GraphState("Source"),
                new GraphState("Destination"),
                new TriggerInfo("Go"),
                new InvocationInfo[0]);

            var exception = Assert.Throws<InvalidOperationException>(() =>
                style.FormatAllTransitions(new List<GraphTransition> { transition }));

            Assert.Contains("FormatAllTransitions", exception.Message);
        }

        private static InvocationInfo Invocation(string methodName)
        {
            return new InvocationInfo(methodName, null, InvocationInfo.Timing.Synchronous);
        }

        private static ActionInfo Action(string methodName)
        {
            return new ActionInfo(Invocation(methodName), null);
        }

        private sealed class RecordingGraphStyle : GraphStyleBase
        {
            public readonly List<TransitionCall> Calls = new List<TransitionCall>();

            public bool ReturnNull { get; set; }

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
                    Actions = actions?.ToArray(),
                    DestinationNodeName = destinationNodeName,
                    Guards = guards?.ToArray()
                });

                return ReturnNull ? null : $"{sourceNodeName}->{destinationNodeName}:{trigger}";
            }
        }

        private sealed class DefaultTransitionStyle : GraphStyleBase
        {
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
        }

        private sealed class UnknownTransition : GraphTransition
        {
            public UnknownTransition(GraphState sourceState, TriggerInfo trigger)
                : base(sourceState, trigger)
            {
            }
        }

        private sealed class LabelledValue
        {
            private readonly string _label;

            public LabelledValue(string label)
            {
                _label = label;
            }

            public override string ToString()
            {
                return _label;
            }
        }

        private sealed class TransitionCall
        {
            public string SourceNodeName { get; set; }

            public string Trigger { get; set; }

            public string[] Actions { get; set; }

            public string DestinationNodeName { get; set; }

            public string[] Guards { get; set; }
        }
    }
}
