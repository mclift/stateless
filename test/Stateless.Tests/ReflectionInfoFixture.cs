using System;
using Stateless.Reflection;
using Xunit;

namespace Stateless.Tests
{
    public class ReflectionInfoFixture
    {
        [Fact]
        public void TriggerInfo_ToString_ReturnsNullStringForNullUnderlyingTrigger()
        {
            var info = new TriggerInfo(null);

            Assert.Null(info.UnderlyingTrigger);
            Assert.Equal(SpecialConstants.NullString, info.ToString());
        }

        [Fact]
        public void TriggerInfo_ToString_UsesUnderlyingTriggerToStringVerbatim()
        {
            var trigger = new LabelledValue("Trigger \"X\"\n[raw]");
            var info = new TriggerInfo(trigger);

            Assert.Same(trigger, info.UnderlyingTrigger);
            Assert.Equal(trigger.ToString(), info.ToString());
        }

        [Fact]
        public void InvocationInfo_UserDescriptionOverridesCompilerGeneratedMethodName()
        {
            var info = new InvocationInfo("<>c__DisplayClass0_0", "raw \"description\"", InvocationInfo.Timing.Asynchronous);

            Assert.Equal("raw \"description\"", info.Description);
            Assert.True(info.IsAsync);
        }

        [Theory]
        [InlineData("<>c__DisplayClass0_0")]
        [InlineData("Method`1")]
        [InlineData("<Lambda>b__0")]
        public void InvocationInfo_CompilerGeneratedMethodNamesUseDefaultDescription(string methodName)
        {
            var info = new InvocationInfo(methodName, null, InvocationInfo.Timing.Synchronous);

            Assert.Equal(InvocationInfo.DefaultFunctionDescription, info.Description);
            Assert.False(info.IsAsync);
        }

        [Fact]
        public void StateMachineInfo_ThrowsArgumentNullExceptionForNullStates()
        {
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new StateMachineInfo(null, typeof(State), typeof(Trigger), null));

            Assert.Equal("states", exception.ParamName);
        }

        [Fact]
        public void DynamicStateInfos_AddGeneric_UsesDestinationToStringVerbatim()
        {
            var infos = new DynamicStateInfos();
            var destination = new LabelledValue("Ready \"state\"\n[raw]");

            infos.Add(destination, "criterion \"raw\"");

            var info = Assert.Single(infos);
            Assert.Equal(destination.ToString(), info.DestinationState);
            Assert.Equal("criterion \"raw\"", info.Criterion);
        }

        [Fact]
        public void DynamicTransitionInfo_Create_DefaultsNullGuardsToEmptyAndPreservesMetadata()
        {
            var selector = new InvocationInfo("ChooseState", "selector \"raw\"", InvocationInfo.Timing.Synchronous);
            var possibleStates = new DynamicStateInfos
            {
                { "Ready \"state\"\n[raw]", "criterion \"raw\"" }
            };

            var transition = DynamicTransitionInfo.Create(Trigger.X, null, selector, possibleStates);

            Assert.Equal(Trigger.X, (Trigger)transition.Trigger.UnderlyingTrigger);
            Assert.Empty(transition.GuardConditionsMethodDescriptions);
            Assert.Same(selector, transition.DestinationStateSelectorDescription);
            Assert.Same(possibleStates, transition.PossibleDestinationStates);
        }

        [Fact]
        public void GetInfoReflectsDynamicTransitionPossibleDestinationStatesVerbatim()
        {
            var possibleStates = new DynamicStateInfos
            {
                { "Ready \"state\"\n[raw]", "criterion \"raw\"" }
            };
            var sm = new StateMachine<string, string>("Idle");
            sm.Configure("Idle")
                .PermitDynamic("go \"raw\"", () => "Ready \"state\"\n[raw]", "selector \"raw\"", possibleStates);

            var stateInfo = Assert.Single(sm.GetInfo().States);
            var transition = Assert.Single(stateInfo.DynamicTransitions);
            var destination = Assert.Single(transition.PossibleDestinationStates);

            Assert.Equal("selector \"raw\"", transition.DestinationStateSelectorDescription.Description);
            Assert.Equal("go \"raw\"", transition.Trigger.ToString());
            Assert.Same(possibleStates, transition.PossibleDestinationStates);
            Assert.Equal("Ready \"state\"\n[raw]", destination.DestinationState);
            Assert.Equal("criterion \"raw\"", destination.Criterion);
        }

        [Fact]
        public void GetInfoReflectsReentryAsNonInternalFixedTransitionToSameState()
        {
            var sm = new StateMachine<State, Trigger>(State.A);
            sm.Configure(State.A)
                .PermitReentryIf(Trigger.X, () => true, "reentry guard");

            var stateInfo = Assert.Single(sm.GetInfo().States);
            var transition = Assert.Single(stateInfo.FixedTransitions);

            Assert.False(transition.IsInternalTransition);
            Assert.Equal(Trigger.X, (Trigger)transition.Trigger.UnderlyingTrigger);
            Assert.Equal(State.A, (State)transition.DestinationState.UnderlyingState);
            Assert.Equal("reentry guard", Assert.Single(transition.GuardConditionsMethodDescriptions).Description);
        }

        [Fact]
        public void GetInfoReflectsInternalTransitionAsInternalFixedTransition()
        {
            var sm = new StateMachine<State, Trigger>(State.A);
            sm.Configure(State.A)
                .InternalTransitionIf(Trigger.X, args => true, t => { }, "internal guard");

            var stateInfo = Assert.Single(sm.GetInfo().States);
            var transition = Assert.Single(stateInfo.FixedTransitions);

            Assert.True(transition.IsInternalTransition);
            Assert.Equal(Trigger.X, (Trigger)transition.Trigger.UnderlyingTrigger);
            Assert.Equal(State.A, (State)transition.DestinationState.UnderlyingState);
            Assert.Equal("internal guard", Assert.Single(transition.GuardConditionsMethodDescriptions).Description);
        }

        [Fact]
        public void GetInfoReflectsIgnoredTransitionGuardDescriptions()
        {
            var sm = new StateMachine<State, Trigger>(State.A);
            sm.Configure(State.A)
                .IgnoreIf(Trigger.X, () => false, "ignore guard");

            var stateInfo = Assert.Single(sm.GetInfo().States);
            var ignored = Assert.Single(stateInfo.IgnoredTriggers);

            Assert.False(ignored.IsInternalTransition);
            Assert.Equal(Trigger.X, (Trigger)ignored.Trigger.UnderlyingTrigger);
            Assert.Equal("ignore guard", Assert.Single(ignored.GuardConditionsMethodDescriptions).Description);
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
    }
}
