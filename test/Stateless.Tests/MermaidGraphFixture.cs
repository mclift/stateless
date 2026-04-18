using System.Text;
using System;
using System.Threading.Tasks;
using Stateless.Reflection;
using Xunit;

namespace Stateless.Tests
{
    public class MermaidGraphFixture
    {
        [Fact]
        public void Format_InitialTransition_ShouldReturns()
        {
            var expected = new StringBuilder()
                .AppendLine("stateDiagram-v2")
                .AppendLine("[*] --> A")
                .ToString().TrimEnd();

            var sm = new StateMachine<State, Trigger>(State.A);

            var result = Graph.MermaidGraph.Format(sm.GetInfo());

            WriteToFile(nameof(Format_InitialTransition_ShouldReturns), result);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void SimpleTransition()
        {
            var expected = new StringBuilder()
                .AppendLine("stateDiagram-v2")
                .AppendLine("	A --> B : X")
                .AppendLine("[*] --> A")
                .ToString().TrimEnd();

            var sm = new StateMachine<State, Trigger>(State.A);

            sm.Configure(State.A)
                .Permit(Trigger.X, State.B);

            var result = Graph.MermaidGraph.Format(sm.GetInfo());

            WriteToFile(nameof(SimpleTransition), result);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void SimpleTransition_LeftToRight()
        {
            var expected = new StringBuilder()
                .AppendLine("stateDiagram-v2")
                .AppendLine("	direction LR")
                .AppendLine("	A --> B : X")
                .AppendLine("[*] --> A")
                .ToString().TrimEnd();

            var sm = new StateMachine<State, Trigger>(State.A);

            sm.Configure(State.A)
                .Permit(Trigger.X, State.B);

            var result = Graph.MermaidGraph.Format(sm.GetInfo(), Graph.MermaidGraphDirection.LeftToRight);

            WriteToFile(nameof(SimpleTransition_LeftToRight), result);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(Graph.MermaidGraphDirection.RightToLeft, "RL")]
        [InlineData(Graph.MermaidGraphDirection.TopToBottom, "TB")]
        [InlineData(Graph.MermaidGraphDirection.BottomToTop, "BT")]
        public void SimpleTransition_WithDirection_IncludesDirectionCode(Graph.MermaidGraphDirection direction, string directionCode)
        {
            var expected = new StringBuilder()
                .AppendLine("stateDiagram-v2")
                .AppendLine($"	direction {directionCode}")
                .AppendLine("	A --> B : X")
                .AppendLine("[*] --> A")
                .ToString().TrimEnd();

            var sm = new StateMachine<State, Trigger>(State.A);

            sm.Configure(State.A)
                .Permit(Trigger.X, State.B);

            var result = Graph.MermaidGraph.Format(sm.GetInfo(), direction);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void Format_WithUnsupportedDirection_ThrowsArgumentOutOfRangeException()
        {
            var sm = new StateMachine<State, Trigger>(State.A);

            var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
                Graph.MermaidGraph.Format(sm.GetInfo(), (Graph.MermaidGraphDirection)999));

            Assert.Equal("direction", exception.ParamName);
        }

        [Fact]
        public void TwoSimpleTransitions()
        {
            var expected = new StringBuilder()
                .AppendLine("stateDiagram-v2")
                .AppendLine("	A --> B : X") 
                .AppendLine("	A --> C : Y")
                .AppendLine("[*] --> A")
                .ToString().TrimEnd();

            var sm = new StateMachine<State, Trigger>(State.A);

            sm.Configure(State.A)
                   .Permit(Trigger.X, State.B)
                   .Permit(Trigger.Y, State.C);

            var result = Graph.MermaidGraph.Format(sm.GetInfo());

            WriteToFile(nameof(TwoSimpleTransitions), result);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void WhenDiscriminatedByAnonymousGuard()
        {
            var expected = new StringBuilder()
                .AppendLine("stateDiagram-v2")
                .AppendLine("	A --> B : X [Function]")
                .AppendLine("[*] --> A")
                .ToString().TrimEnd();

            bool anonymousGuard() => true;

            var sm = new StateMachine<State, Trigger>(State.A);

            sm.Configure(State.A)
                .PermitIf(Trigger.X, State.B, anonymousGuard);
            sm.Configure(State.B);

            var result = Graph.MermaidGraph.Format(sm.GetInfo());

            WriteToFile(nameof(WhenDiscriminatedByAnonymousGuard), result);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void WhenDiscriminatedByAnonymousGuardWithDescription()
        {
            var expected = new StringBuilder()
                .AppendLine("stateDiagram-v2")
                .AppendLine("	A --> B : X [description]")
                .AppendLine("[*] --> A")
                .ToString().TrimEnd();

            bool guardFunction() => true;

            var sm = new StateMachine<State, Trigger>(State.A);

            sm.Configure(State.A)
                .PermitIf(Trigger.X, State.B, guardFunction, "description");
            sm.Configure(State.B);

            var result = Graph.MermaidGraph.Format(sm.GetInfo());

            WriteToFile(nameof(WhenDiscriminatedByAnonymousGuard), result);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void WhenDiscriminatedByNamedDelegate()
        {
            var expected = new StringBuilder()
                .AppendLine("stateDiagram-v2")
                .AppendLine("	A --> B : X [IsTrue]")
                .AppendLine("[*] --> A")
                .ToString().TrimEnd();

            var sm = new StateMachine<State, Trigger>(State.A);

            sm.Configure(State.A)
                .PermitIf(Trigger.X, State.B, IsTrue);

            var result = Graph.MermaidGraph.Format(sm.GetInfo());

            WriteToFile(nameof(WhenDiscriminatedByNamedDelegate), result);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void WhenDiscriminatedByNamedDelegateWithDescription()
        {
            var expected = new StringBuilder()
                .AppendLine("stateDiagram-v2")
                .AppendLine("	A --> B : X [description]")
                .AppendLine("[*] --> A")
                .ToString().TrimEnd();

            var sm = new StateMachine<State, Trigger>(State.A);

            sm.Configure(State.A)
                .PermitIf(Trigger.X, State.B, IsTrue, "description");

            var result = Graph.MermaidGraph.Format(sm.GetInfo());

            WriteToFile(nameof(WhenDiscriminatedByNamedDelegateWithDescription), result);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void DestinationStateIsDynamic()
        {
            var expected = new StringBuilder()
                .AppendLine("stateDiagram-v2")
                .AppendLine("	state Decision1 <<choice>>")
                .AppendLine("	A --> Decision1 : X")
                .AppendLine("[*] --> A")
                .ToString().TrimEnd();

            var sm = new StateMachine<State, Trigger>(State.A);
            sm.Configure(State.A)
                .PermitDynamic(Trigger.X, () => State.B);

            var result = Graph.MermaidGraph.Format(sm.GetInfo());

            WriteToFile(nameof(DestinationStateIsDynamic), result);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void DynamicTransitionWithPossibleDestinationStates()
        {
            var expected = new StringBuilder()
                .AppendLine("stateDiagram-v2")
                .AppendLine("	state Decision1 <<choice>>")
                .AppendLine("	A --> Decision1 : X")
                .AppendLine("	Decision1 --> B : X [ChoseB]")
                .AppendLine("	Decision1 --> C : X [ChoseC]")
                .AppendLine("[*] --> A")
                .ToString().TrimEnd();

            var sm = new StateMachine<State, Trigger>(State.A);
            sm.Configure(State.A)
                .PermitDynamic(Trigger.X, DestinationSelector, null, new DynamicStateInfos
                {
                    { State.B, "ChoseB" },
                    { State.C, "ChoseC" }
                });

            sm.Configure(State.B);
            sm.Configure(State.C);

            var result = Graph.MermaidGraph.Format(sm.GetInfo());

            Assert.Equal(expected, result);
        }

        [Fact]
        public void DynamicAsyncTransitionWithPossibleDestinationStates()
        {
            var expected = new StringBuilder()
                .AppendLine("stateDiagram-v2")
                .AppendLine("	state Decision1 <<choice>>")
                .AppendLine("	A --> Decision1 : X")
                .AppendLine("	Decision1 --> B : X [ChoseB]")
                .AppendLine("	Decision1 --> C : X [ChoseC]")
                .AppendLine("[*] --> A")
                .ToString().TrimEnd();

            var sm = new StateMachine<State, Trigger>(State.A);
            sm.Configure(State.A)
                .PermitDynamicAsync(Trigger.X, () => Task.FromResult(DestinationSelector()), null, new DynamicStateInfos
                {
                    { State.B, "ChoseB" },
                    { State.C, "ChoseC" }
                });

            sm.Configure(State.B);
            sm.Configure(State.C);

            var result = Graph.MermaidGraph.Format(sm.GetInfo());

            Assert.Equal(expected, result);
        }

        [Fact]
        public void DynamicAsyncTransitionWithGuardIncludesGuardDescription()
        {
            var expected = new StringBuilder()
                .AppendLine("stateDiagram-v2")
                .AppendLine("	state Decision1 <<choice>>")
                .AppendLine("	A --> Decision1 : X [Allowed]")
                .AppendLine("	Decision1 --> B : X [ChoseB]")
                .AppendLine("[*] --> A")
                .ToString().TrimEnd();

            var sm = new StateMachine<State, Trigger>(State.A);
            sm.Configure(State.A)
                .PermitDynamicIfAsync(
                    Trigger.X,
                    () => Task.FromResult(DestinationSelector()),
                    () => true,
                    "Allowed",
                    new DynamicStateInfos
                    {
                        { State.B, "ChoseB" }
                    });

            sm.Configure(State.B);

            var result = Graph.MermaidGraph.Format(sm.GetInfo());

            Assert.Equal(expected, result);
        }

        [Fact]
        public void DynamicDecisionNodeDescriptionIsNotRenderedAsMermaidLabel()
        {
            var expected = new StringBuilder()
                .AppendLine("stateDiagram-v2")
                .AppendLine("	state Decision1 <<choice>>")
                .AppendLine("	A --> Decision1 : X")
                .AppendLine("[*] --> A")
                .ToString().TrimEnd();

            var sm = new StateMachine<State, Trigger>(State.A);
            sm.Configure(State.A)
                .PermitDynamic(
                    Trigger.X,
                    () => State.B,
                    "InjectedLabel\nstate Injected <<choice>>");

            var result = Graph.MermaidGraph.Format(sm.GetInfo());

            Assert.Equal(expected, result);
            Assert.DoesNotContain("InjectedLabel", result);
            Assert.DoesNotContain("Injected <<choice>>", result);
        }

        [Fact]
        public void DestinationStateIsCalculatedBasedOnTriggerParameters()
        {
            var expected = new StringBuilder()
                .AppendLine("stateDiagram-v2")
                .AppendLine("	state Decision1 <<choice>>")
                .AppendLine("	A --> Decision1 : X")
                .AppendLine("[*] --> A")
                .ToString().TrimEnd();

            var sm = new StateMachine<State, Trigger>(State.A);
            var trigger = sm.SetTriggerParameters<int>(Trigger.X);
            sm.Configure(State.A)
                .PermitDynamic(trigger, i => i == 1 ? State.B : State.C);

            var result = Graph.MermaidGraph.Format(sm.GetInfo());

            WriteToFile(nameof(DestinationStateIsCalculatedBasedOnTriggerParameters), result);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void TransitionWithIgnore()
        {
            // This test duplicates the behaviour expressed in the TransitionWithIgnore test in DotGraphFixture, 
            // but it seems counter-intuitive to show the ignored trigger as a transition back to the same state.
            var expected = new StringBuilder()
                .AppendLine("stateDiagram-v2")
                .AppendLine("	A --> B : X")
                .AppendLine("	A --> A : Y")
                .AppendLine("[*] --> A")
                .ToString().TrimEnd();

            var sm = new StateMachine<State, Trigger>(State.A);

            sm.Configure(State.A)
                .Ignore(Trigger.Y)
                .Permit(Trigger.X, State.B);

            var result = Graph.MermaidGraph.Format(sm.GetInfo());

            WriteToFile(nameof(TransitionWithIgnore), result);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void ReentrantTransitionShowsDefaultEntryAction()
        {
            var expected = new StringBuilder()
                .AppendLine("stateDiagram-v2")
                .AppendLine("	A --> A : X / EnterA")
                .AppendLine("[*] --> A")
                .ToString().TrimEnd();

            var sm = new StateMachine<State, Trigger>(State.A);

            sm.Configure(State.A)
                .OnEntry(TestEntryAction, "EnterA")
                .PermitReentry(Trigger.X);

            var result = Graph.MermaidGraph.Format(sm.GetInfo());

            Assert.Equal(expected, result);
        }

        [Fact]
        public void InternalTransitionDoesNotShowEntryOrExitActions()
        {
            var expected = new StringBuilder()
                .AppendLine("stateDiagram-v2")
                .AppendLine("	A --> A : X [Function]")
                .AppendLine("[*] --> A")
                .ToString().TrimEnd();

            var sm = new StateMachine<State, Trigger>(State.A);

            sm.Configure(State.A)
                .OnEntry(TestEntryAction, "EnterA")
                .OnExit(TestEntryAction, "ExitA")
                .InternalTransition(Trigger.X, x => { });

            var result = Graph.MermaidGraph.Format(sm.GetInfo());

            Assert.Equal(expected, result);
        }

        [Fact]
        public void OnEntryWithTriggerParameter()
        {
            var expected = new StringBuilder()
                .AppendLine("stateDiagram-v2")
                .AppendLine("	A --> B : X / BX")
                .AppendLine("	A --> C : Y / TestEntryActionString [IsTriggerY]")
                .AppendLine("	A --> B : Z [IsTriggerZ]")
                .AppendLine("[*] --> A")
                .ToString().TrimEnd();

            bool anonymousGuard() => true;
            var sm = new StateMachine<State, Trigger>(State.A);
            var parmTrig = sm.SetTriggerParameters<string>(Trigger.Y);

            sm.Configure(State.A)
                .OnEntry(() => { }, "OnEntry")
                .Permit(Trigger.X, State.B)
                .PermitIf(Trigger.Y, State.C, anonymousGuard, "IsTriggerY")
                .PermitIf(Trigger.Z, State.B, anonymousGuard, "IsTriggerZ");

            sm.Configure(State.B)
                .OnEntryFrom(Trigger.X, TestEntryAction, "BX");

            sm.Configure(State.C)
                .OnEntryFrom(parmTrig, TestEntryActionString);

            var result = Graph.MermaidGraph.Format(sm.GetInfo());

            WriteToFile(nameof(TransitionWithIgnore), result);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void SpacedWithSubstate()
        {
            string StateA = "State A";
            string StateB = "State B";
            string StateC = "State C";
            string StateD = "State D";
            string TriggerX = "Trigger X";
            string TriggerY = "Trigger Y";

            var expected = new StringBuilder()
                .AppendLine("stateDiagram-v2")
                .AppendLine("	StateD : State D")
                .AppendLine("	StateB : State B")
                .AppendLine("	StateC : State C")
                .AppendLine("	StateA : State A")
                .AppendLine("	state StateD {")
                .AppendLine("		StateB")
                .AppendLine("		StateC")
                .AppendLine("	}")
                .AppendLine("	StateA --> StateB : Trigger X")
                .AppendLine("	StateA --> StateC : Trigger Y")
                .AppendLine("[*] --> StateA")
                .ToString().TrimEnd();

            var sm = new StateMachine<string, string>("State A");

            sm.Configure(StateA)
                .Permit(TriggerX, StateB)
                .Permit(TriggerY, StateC)
                .OnEntry(TestEntryAction, "Enter A")
                .OnExit(TestEntryAction, "Exit A");

            sm.Configure(StateB)
                .SubstateOf(StateD);
            sm.Configure(StateC)
                .SubstateOf(StateD);
            sm.Configure(StateD)
                .OnEntry(TestEntryAction, "Enter D");

            var result = Graph.MermaidGraph.Format(sm.GetInfo());

            WriteToFile(nameof(SpacedWithSubstate), result);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void WithSubstate()
        {
            var expected = new StringBuilder()
                .AppendLine("stateDiagram-v2")
                .AppendLine("	state D {")
                .AppendLine("		B")
                .AppendLine("		C")
                .AppendLine("	}")
                .AppendLine("	A --> B : X")
                .AppendLine("	A --> C : Y")
                .AppendLine("[*] --> A")
                .ToString().TrimEnd();

            var sm = new StateMachine<State, Trigger>(State.A);

            sm.Configure(State.A)
                .Permit(Trigger.X, State.B)
                .Permit(Trigger.Y, State.C);

            sm.Configure(State.B)
                .SubstateOf(State.D);
            sm.Configure(State.C)
                .SubstateOf(State.D);
            sm.Configure(State.D);

            var result = Graph.MermaidGraph.Format(sm.GetInfo());

            WriteToFile(nameof(WithSubstate), result);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void StateNamesWithColonDashAndWhitespaceAreAliasedThroughoutGraph()
        {
            var expected = new StringBuilder()
                .AppendLine("stateDiagram-v2")
                .AppendLine("	ABC : A:B - C")
                .AppendLine("	BCD : B:C-D")
                .AppendLine("	ABC --> BCD : Go")
                .AppendLine("[*] --> ABC")
                .ToString().TrimEnd();

            var sm = new StateMachine<string, string>("A:B - C");

            sm.Configure("A:B - C")
                .Permit("Go", "B:C-D");
            sm.Configure("B:C-D");

            var result = Graph.MermaidGraph.Format(sm.GetInfo());

            Assert.Equal(expected, result);
        }

        [Fact]
        public void StateNamesWithSpacesAreAliased()
        {
            var expected = new StringBuilder()
                .AppendLine("stateDiagram-v2")
                .AppendLine("	AA : A A")
                .AppendLine("	AA_1 : A  A")
                .AppendLine("	AA_2 : A   A")
                .AppendLine("	AA --> B : X")
                .AppendLine("	AA_1 --> B : X")
                .AppendLine("	AA_2 --> B : X")
                .AppendLine("[*] --> AA")
                .ToString().TrimEnd();

            var sm = new StateMachine<string, Trigger>("A A");

            sm.Configure("A A").Permit(Trigger.X, "B");
            sm.Configure("A  A").Permit(Trigger.X, "B");
            sm.Configure("A   A").Permit(Trigger.X, "B");

            var result = Graph.MermaidGraph.Format(sm.GetInfo());

            WriteToFile(nameof(StateNamesWithSpacesAreAliased), result);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void StateNamesSanitizedToExistingStateNameAreUniquelyAliased()
        {
            var expected = new StringBuilder()
                .AppendLine("stateDiagram-v2")
                .AppendLine("	AB_1 : A-B")
                .AppendLine("	AB_1 --> AB : Go")
                .AppendLine("[*] --> AB_1")
                .ToString().TrimEnd();

            var sm = new StateMachine<string, string>("A-B");

            sm.Configure("A-B").Permit("Go", "AB");
            sm.Configure("AB");

            var result = Graph.MermaidGraph.Format(sm.GetInfo());

            Assert.Equal(expected, result);
        }

        [Fact]
        public void MermaidGraphStyleGetPrefixCanBeCalledMoreThanOnce()
        {
            var sm = new StateMachine<string, string>("A A");
            sm.Configure("A A").Permit("Go", "B");

            var graph = new Graph.StateGraph(sm.GetInfo());
            var style = new Graph.MermaidGraphStyle(graph, null);

            var firstPrefix = style.GetPrefix();
            var secondPrefix = style.GetPrefix();

            Assert.Equal(firstPrefix, secondPrefix);
            Assert.Contains("	AA : A A", secondPrefix);
        }

        private bool IsTrue()
        {
            return true;
        }

        private void TestEntryAction() { }

        private void TestEntryActionString(string val) { }

        private State DestinationSelector() { return State.A; }

        private void WriteToFile(string fileName, string content)
        {
#if WRITE_DOTS_TO_FOLDER
            System.IO.File.WriteAllText(System.IO.Path.Combine("c:\\temp", $"{fileName}.txt"), content);
#endif
        }
    }
}
