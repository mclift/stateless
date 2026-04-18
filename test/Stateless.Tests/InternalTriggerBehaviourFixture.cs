using System;
using System.Threading.Tasks;
using Xunit;

namespace Stateless.Tests
{
    public class InternalTriggerBehaviourFixture
    {
        [Fact]
        public void SyncExecuteRunsInternalActionWithTransitionAndArgs()
        {
            var transition = new StateMachine<State, Trigger>.Transition(State.A, State.A, Trigger.X);
            var args = new object[] { "payload" };
            StateMachine<State, Trigger>.Transition capturedTransition = null;
            object[] capturedArgs = null;
            var behaviour = new StateMachine<State, Trigger>.InternalTriggerBehaviour.Sync(
                Trigger.X,
                _ => true,
                (t, a) =>
                {
                    capturedTransition = t;
                    capturedArgs = a;
                });

            behaviour.Execute(transition, args);

            Assert.Equal(Trigger.X, behaviour.Trigger);
            Assert.True(behaviour.GuardConditionsMet());
            Assert.Same(transition, capturedTransition);
            Assert.Same(args, capturedArgs);
        }

        [Fact]
        public async Task SyncExecuteAsyncRunsInternalActionAndCompletes()
        {
            var invoked = false;
            var behaviour = new StateMachine<State, Trigger>.InternalTriggerBehaviour.Sync(
                Trigger.X,
                _ => true,
                (t, a) => invoked = true);

            var task = behaviour.ExecuteAsync(new StateMachine<State, Trigger>.Transition(State.A, State.A, Trigger.X), new object[0]);

            Assert.True(task.IsCompleted);
            await task;
            Assert.True(invoked);
        }

        [Fact]
        public void SyncGuardDescriptionIsReportedWhenGuardFails()
        {
            var behaviour = new StateMachine<State, Trigger>.InternalTriggerBehaviour.Sync(
                Trigger.X,
                _ => false,
                (t, a) => { },
                "blocked");

            Assert.False(behaviour.GuardConditionsMet());
            Assert.Equal(new[] { "blocked" }, behaviour.UnmetGuardConditions(new object[0]));
        }

        [Fact]
        public async Task AsyncExecuteAsyncReturnsInternalActionTaskAndPassesArguments()
        {
            var transition = new StateMachine<State, Trigger>.Transition(State.A, State.A, Trigger.X);
            var args = new object[] { "payload" };
            var completion = new TaskCompletionSource<int>();
            StateMachine<State, Trigger>.Transition capturedTransition = null;
            object[] capturedArgs = null;
            var behaviour = new StateMachine<State, Trigger>.InternalTriggerBehaviour.Async(
                Trigger.X,
                _ => true,
                (t, a) =>
                {
                    capturedTransition = t;
                    capturedArgs = a;
                    return completion.Task;
                });

            var task = behaviour.ExecuteAsync(transition, args);

            Assert.Same(completion.Task, task);
            Assert.Same(transition, capturedTransition);
            Assert.Same(args, capturedArgs);

            completion.SetResult(0);
            await task;
        }

        [Fact]
        public void AsyncExecuteThrowsWhenCalledSynchronously()
        {
            var transition = new StateMachine<State, Trigger>.Transition(State.A, State.B, Trigger.X);
            var behaviour = new StateMachine<State, Trigger>.InternalTriggerBehaviour.Async(
                Trigger.X,
                _ => true,
                (t, a) => TaskResult.Done);

            var exception = Assert.Throws<InvalidOperationException>(() => behaviour.Execute(transition, new object[0]));

            Assert.Contains("'B' state", exception.Message);
            Assert.Contains("FireAsync", exception.Message);
        }

        [Fact]
        [Obsolete]
        public void AsyncDeprecatedConstructorUsesNoArgumentGuard()
        {
            var guardCalls = 0;
            var behaviour = new StateMachine<State, Trigger>.InternalTriggerBehaviour.Async(
                Trigger.X,
                () =>
                {
                    guardCalls++;
                    return true;
                },
                (t, a) => TaskResult.Done);

            Assert.True(behaviour.GuardConditionsMet("ignored"));
            Assert.Equal(1, guardCalls);
        }
    }
}
