using System;
using System.Threading.Tasks;
using Xunit;

namespace Stateless.Tests
{
    public class TriggerBehaviourFixture
    {
        [Fact]
        public void ExposesCorrectUnderlyingTrigger()
        {
            var transitioning = new StateMachine<State, Trigger>.TransitioningTriggerBehaviour(
                Trigger.X, State.C, null);

            Assert.Equal(Trigger.X, transitioning.Trigger);
        }

        [Fact]
        public void ReentryTriggerBehaviourExposesDestinationAndUsesEmptyGuardWhenNull()
        {
            var reentry = new StateMachine<State, Trigger>.ReentryTriggerBehaviour(
                Trigger.X, State.C, null);

            Assert.Equal(Trigger.X, reentry.Trigger);
            Assert.Equal(State.C, reentry.Destination);
            Assert.True(reentry.GuardConditionsMet());
            Assert.Empty(reentry.Guards);
        }

        [Fact]
        public async Task ReentryTriggerBehaviourAsyncExposesDestinationAndEvaluatesGuard()
        {
            var reentry = new StateMachine<State, Trigger>.ReentryTriggerBehaviourAsync(
                Trigger.X,
                State.C,
                new StateMachine<State, Trigger>.TransitionGuardAsync(args => Task.FromResult((int)args[0] == 1), "only one"));

            Assert.Equal(Trigger.X, reentry.Trigger);
            Assert.Equal(State.C, reentry.Destination);
            Assert.True(await reentry.GuardConditionsMet(1));
            Assert.False(await reentry.GuardConditionsMet(2));
            Assert.Equal(new[] { "only one" }, await reentry.UnmetGuardConditions(new object[] { 2 }));
        }

        protected bool False(params object[] args)
        {
            return false;
        }

        [Fact]
        public void WhenGuardConditionFalse_GuardConditionsMetIsFalse()
        {
            var transitioning = new StateMachine<State, Trigger>.TransitioningTriggerBehaviour(
                Trigger.X, State.C, new StateMachine<State, Trigger>.TransitionGuard(False));

            Assert.False(transitioning.GuardConditionsMet());
        }

        protected bool True(params object[] args)
        {
            return true;
        }

        [Fact]
        public void WhenGuardConditionTrue_GuardConditionsMetIsTrue()
        {
            var transitioning = new StateMachine<State, Trigger>.TransitioningTriggerBehaviour(
                Trigger.X, State.C, new StateMachine<State, Trigger>.TransitionGuard(True));

            Assert.True(transitioning.GuardConditionsMet());
        }

        [Fact]
        public void WhenOneOfMultipleGuardConditionsFalse_GuardConditionsMetIsFalse()
        {
            var falseGuard = new[] {
                new Tuple<Func<object[], bool>, string>(args => true, "1"),
                new Tuple<Func<object[], bool>, string>(args => true, "2")
            };

            var transitioning = new StateMachine<State, Trigger>.TransitioningTriggerBehaviour(
                Trigger.X, State.C, new StateMachine<State, Trigger>.TransitionGuard(falseGuard));

            Assert.True(transitioning.GuardConditionsMet());
        }

        [Fact]
        public void WhenAllMultipleGuardConditionsFalse_IsGuardConditionsMetIsFalse()
        {
            var falseGuard = new[] {
                new Tuple<Func<object[], bool>, string>(args => false, "1"),
                new Tuple<Func<object[], bool>, string>(args => false, "2")
            };

            var transitioning = new StateMachine<State, Trigger>.TransitioningTriggerBehaviour(
                Trigger.X, State.C, new StateMachine<State, Trigger>.TransitionGuard(falseGuard));

            Assert.False(transitioning.GuardConditionsMet());
        }

        [Fact]
        public void WhenAllGuardConditionsTrue_GuardConditionsMetIsTrue()
        {
            var trueGuard = new[] {
                new Tuple<Func<object[], bool>, string>(args => true, "1"),
                new Tuple<Func<object[], bool>, string>(args => true, "2")
            };

            var transitioning = new StateMachine<State, Trigger>.TransitioningTriggerBehaviour(
                Trigger.X, State.C, new StateMachine<State, Trigger>.TransitionGuard(trueGuard));

            Assert.True(transitioning.GuardConditionsMet());
        }
    }
}
