using System;
using System.Collections.Generic;
using System.Linq;
using Stateless.Reflection;
using Xunit;
using GraphState = Stateless.Graph.State;
using GraphStateGraph = Stateless.Graph.StateGraph;
using GraphStyleBase = Stateless.Graph.GraphStyleBase;
using GraphSuperState = Stateless.Graph.SuperState;

namespace Stateless.Tests
{
    public class StateGraphFixture
    {
        [Fact]
        public void ToGraphFormatsClustersStatesDecisionsTransitionsAndInitialTransitionInOrder()
        {
            var sm = new StateMachine<State, Trigger>(State.A);
            sm.Configure(State.A)
                .PermitDynamic(Trigger.X, DestinationSelector, null, new DynamicStateInfos
                {
                    { State.B, "ChoseB" },
                    { State.C, "ChoseC" }
                });

            sm.Configure(State.B).SubstateOf(State.D);
            sm.Configure(State.C).SubstateOf(State.D);
            sm.Configure(State.D);

            var graph = new GraphStateGraph(sm.GetInfo());
            var style = new RecordingGraphStyle();

            var result = graph.ToGraph(style);

            var expected = string.Join(Environment.NewLine, new[]
            {
                "prefix|cluster:D:B,C|state:A|decision:Decision1:DestinationSelector",
                "transition:A->Decision1:X::",
                "transition:Decision1->B:X::ChoseB",
                "transition:Decision1->C:X::ChoseC",
                "initial:A"
            });

            Assert.Equal(expected, result);
            Assert.Equal(new[]
            {
                "prefix",
                "cluster:D:B,C",
                "state:A",
                "decision:Decision1:DestinationSelector",
                "transition:A->Decision1:X::",
                "transition:Decision1->B:X::ChoseB",
                "transition:Decision1->C:X::ChoseC",
                "initial:A"
            }, style.Calls);
        }

        [Fact]
        public void ConstructorBuildsNestedSuperstateHierarchy()
        {
            var sm = new StateMachine<string, string>("Leaf");
            sm.Configure("Leaf")
                .SubstateOf("Middle");
            sm.Configure("Middle")
                .SubstateOf("Root");
            sm.Configure("Root");

            var graph = new GraphStateGraph(sm.GetInfo());

            var root = Assert.IsType<GraphSuperState>(graph.States["Root"]);
            var middle = Assert.IsType<GraphSuperState>(graph.States["Middle"]);
            var leaf = graph.States["Leaf"];

            Assert.Null(root.SuperState);
            Assert.Same(root, middle.SuperState);
            Assert.Same(middle, leaf.SuperState);
            Assert.Equal(new[] { middle }, root.SubStates);
            Assert.Equal(new[] { leaf }, middle.SubStates);
        }

        private State DestinationSelector()
        {
            return State.B;
        }

        private sealed class RecordingGraphStyle : GraphStyleBase
        {
            public readonly List<string> Calls = new List<string>();

            public override string GetPrefix()
            {
                Calls.Add("prefix");
                return "prefix";
            }

            public override string GetInitialTransition(StateInfo initialState)
            {
                var line = $"initial:{initialState.UnderlyingState}";
                Calls.Add(line);
                return Environment.NewLine + line;
            }

            public override string FormatOneState(GraphState state)
            {
                var line = $"state:{state.StateName}";
                Calls.Add(line);
                return "|" + line;
            }

            public override string FormatOneCluster(GraphSuperState stateInfo)
            {
                var line = $"cluster:{stateInfo.StateName}:{string.Join(",", stateInfo.SubStates.Select(x => x.StateName))}";
                Calls.Add(line);
                return "|" + line;
            }

            public override string FormatOneDecisionNode(string nodeName, string label)
            {
                var line = $"decision:{nodeName}:{label}";
                Calls.Add(line);
                return "|" + line;
            }

            public override string FormatOneTransition(
                string sourceNodeName,
                string trigger,
                IEnumerable<string> actions,
                string destinationNodeName,
                IEnumerable<string> guards)
            {
                var line = $"transition:{sourceNodeName}->{destinationNodeName}:{trigger}:{string.Join(",", actions ?? Enumerable.Empty<string>())}:{string.Join(",", guards ?? Enumerable.Empty<string>())}";
                Calls.Add(line);
                return line;
            }
        }
    }
}
