using System;
using System.Threading.Tasks;
using Stateless.Reflection;
using Xunit;

namespace Stateless.Tests
{
    public class DynamicTriggerBehaviourInternalsFixture
    {
        [Fact]
        public void DynamicTriggerBehaviourPassesArgumentsToDestinationSelector()
        {
            object[] capturedArgs = null;
            var expectedArgs = new object[] { 42, "value" };
            var transitionInfo = DynamicTransitionInfo();
            var behaviour = new StateMachine<State, Trigger>.DynamicTriggerBehaviour(
                Trigger.X,
                args =>
                {
                    capturedArgs = args;
                    return State.C;
                },
                null,
                transitionInfo);

            behaviour.GetDestinationState(State.A, expectedArgs, out var destination);

            Assert.Same(expectedArgs, capturedArgs);
            Assert.Same(transitionInfo, behaviour.TransitionInfo);
            Assert.Equal(State.C, destination);
        }

        [Fact]
        public void DynamicTriggerBehaviourThrowsForNullDestinationSelector()
        {
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new StateMachine<State, Trigger>.DynamicTriggerBehaviour(
                    Trigger.X,
                    null,
                    null,
                    DynamicTransitionInfo()));

            Assert.Equal("destination", exception.ParamName);
        }

        [Fact]
        public void DynamicTriggerBehaviourThrowsForNullTransitionInfo()
        {
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new StateMachine<State, Trigger>.DynamicTriggerBehaviour(
                    Trigger.X,
                    args => State.B,
                    null,
                    null));

            Assert.Equal("info", exception.ParamName);
        }

        [Fact]
        public async Task DynamicTriggerBehaviourAsyncPassesArgumentsToDestinationSelector()
        {
            object[] capturedArgs = null;
            var expectedArgs = new object[] { 42, "value" };
            var transitionInfo = DynamicTransitionInfo();
            var behaviour = new StateMachine<State, Trigger>.DynamicTriggerBehaviourAsync(
                Trigger.X,
                args =>
                {
                    capturedArgs = args;
                    return Task.FromResult(State.C);
                },
                null,
                transitionInfo);

            var destination = await behaviour.GetDestinationState(State.A, expectedArgs);

            Assert.Same(expectedArgs, capturedArgs);
            Assert.Same(transitionInfo, behaviour.TransitionInfo);
            Assert.Equal(State.C, destination);
        }

        [Fact]
        public async Task DynamicTriggerBehaviourAsyncGetDestinationStateWaitsForSelectorTask()
        {
            object[] capturedArgs = null;
            var expectedArgs = new object[] { 42, "value" };
            var completion = new TaskCompletionSource<State>();
            var behaviour = new StateMachine<State, Trigger>.DynamicTriggerBehaviourAsync(
                Trigger.X,
                args =>
                {
                    capturedArgs = args;
                    return completion.Task;
                },
                null,
                DynamicTransitionInfo());

            var destinationTask = behaviour.GetDestinationState(State.A, expectedArgs);

            Assert.Same(expectedArgs, capturedArgs);
            Assert.False(destinationTask.IsCompleted);

            completion.SetResult(State.C);
            var destination = await destinationTask;

            Assert.Equal(State.C, destination);
        }

        [Fact]
        public void DynamicTriggerBehaviourAsyncThrowsForNullDestinationSelector()
        {
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new StateMachine<State, Trigger>.DynamicTriggerBehaviourAsync(
                    Trigger.X,
                    null,
                    null,
                    DynamicTransitionInfo()));

            Assert.Equal("destination", exception.ParamName);
        }

        [Fact]
        public void DynamicTriggerBehaviourAsyncThrowsForNullTransitionInfo()
        {
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new StateMachine<State, Trigger>.DynamicTriggerBehaviourAsync(
                    Trigger.X,
                    args => Task.FromResult(State.B),
                    null,
                    null));

            Assert.Equal("info", exception.ParamName);
        }

        private static DynamicTransitionInfo DynamicTransitionInfo()
        {
            return Stateless.Reflection.DynamicTransitionInfo.Create(
                Trigger.X,
                null,
                new InvocationInfo("DestinationSelector", null, InvocationInfo.Timing.Synchronous),
                null);
        }
    }
}
