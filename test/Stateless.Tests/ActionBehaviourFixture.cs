using System;
using System.Threading.Tasks;
using Stateless.Reflection;
using Xunit;

namespace Stateless.Tests
{
    public class ActionBehaviourFixture
    {
        [Fact]
        public async Task ActivateSyncExecuteAsyncRunsAction()
        {
            var invoked = false;
            var behaviour = new StateMachine<State, Trigger>.ActivateActionBehaviour.Sync(
                State.A,
                () => invoked = true,
                Invocation("Activate"));

            await behaviour.ExecuteAsync();

            Assert.True(invoked);
        }

        [Fact]
        public async Task ActivateAsyncExecuteAsyncReturnsActionTask()
        {
            var completion = new TaskCompletionSource<int>();
            var behaviour = new StateMachine<State, Trigger>.ActivateActionBehaviour.Async(
                State.A,
                () => completion.Task,
                Invocation("ActivateAsync"));

            var task = behaviour.ExecuteAsync();

            Assert.Same(completion.Task, task);
            Assert.False(task.IsCompleted);

            completion.SetResult(0);
            await task;
        }

        [Fact]
        public void ActivateAsyncExecuteThrowsWhenCalledSynchronously()
        {
            var behaviour = new StateMachine<State, Trigger>.ActivateActionBehaviour.Async(
                State.A,
                () => TaskResult.Done,
                Invocation("ActivateAsync"));

            var exception = Assert.Throws<InvalidOperationException>(() => behaviour.Execute());

            Assert.Contains("'A' state", exception.Message);
            Assert.Contains("ActivateAsync", exception.Message);
        }

        [Fact]
        public void ActivateActionBehaviourThrowsForNullDescription()
        {
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new StateMachine<State, Trigger>.ActivateActionBehaviour.Sync(State.A, () => { }, null));

            Assert.Equal("actionDescription", exception.ParamName);
        }

        [Fact]
        public async Task DeactivateSyncExecuteAsyncRunsAction()
        {
            var invoked = false;
            var behaviour = new StateMachine<State, Trigger>.DeactivateActionBehaviour.Sync(
                State.A,
                () => invoked = true,
                Invocation("Deactivate"));

            await behaviour.ExecuteAsync();

            Assert.True(invoked);
        }

        [Fact]
        public async Task DeactivateAsyncExecuteAsyncReturnsActionTask()
        {
            var completion = new TaskCompletionSource<int>();
            var behaviour = new StateMachine<State, Trigger>.DeactivateActionBehaviour.Async(
                State.A,
                () => completion.Task,
                Invocation("DeactivateAsync"));

            var task = behaviour.ExecuteAsync();

            Assert.Same(completion.Task, task);
            Assert.False(task.IsCompleted);

            completion.SetResult(0);
            await task;
        }

        [Fact]
        public void DeactivateAsyncExecuteThrowsWhenCalledSynchronously()
        {
            var behaviour = new StateMachine<State, Trigger>.DeactivateActionBehaviour.Async(
                State.A,
                () => TaskResult.Done,
                Invocation("DeactivateAsync"));

            var exception = Assert.Throws<InvalidOperationException>(() => behaviour.Execute());

            Assert.Contains("'A' state", exception.Message);
            Assert.Contains("DeactivateAsync", exception.Message);
        }

        [Fact]
        public void DeactivateActionBehaviourThrowsForNullDescription()
        {
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new StateMachine<State, Trigger>.DeactivateActionBehaviour.Sync(State.A, () => { }, null));

            Assert.Equal("actionDescription", exception.ParamName);
        }

        [Fact]
        public async Task ExitSyncExecuteAsyncRunsAction()
        {
            StateMachine<State, Trigger>.Transition capturedTransition = null;
            var transition = new StateMachine<State, Trigger>.Transition(State.A, State.B, Trigger.X);
            var behaviour = new StateMachine<State, Trigger>.ExitActionBehavior.Sync(
                t => capturedTransition = t,
                Invocation("Exit"));

            await behaviour.ExecuteAsync(transition);

            Assert.Same(transition, capturedTransition);
        }

        [Fact]
        public async Task ExitAsyncExecuteAsyncReturnsActionTaskAndPassesTransition()
        {
            var completion = new TaskCompletionSource<int>();
            var transition = new StateMachine<State, Trigger>.Transition(State.A, State.B, Trigger.X);
            StateMachine<State, Trigger>.Transition capturedTransition = null;
            var behaviour = new StateMachine<State, Trigger>.ExitActionBehavior.Async(
                t =>
                {
                    capturedTransition = t;
                    return completion.Task;
                },
                Invocation("ExitAsync"));

            var task = behaviour.ExecuteAsync(transition);

            Assert.Same(completion.Task, task);
            Assert.Same(transition, capturedTransition);
            Assert.False(task.IsCompleted);

            completion.SetResult(0);
            await task;
        }

        [Fact]
        public void ExitAsyncExecuteThrowsWhenCalledSynchronously()
        {
            var transition = new StateMachine<State, Trigger>.Transition(State.A, State.B, Trigger.X);
            var behaviour = new StateMachine<State, Trigger>.ExitActionBehavior.Async(
                t => TaskResult.Done,
                Invocation("ExitAsync"));

            var exception = Assert.Throws<InvalidOperationException>(() => behaviour.Execute(transition));

            Assert.Contains("'A' state", exception.Message);
            Assert.Contains("FireAsync", exception.Message);
        }

        [Fact]
        public void ExitActionBehaviourThrowsForNullDescription()
        {
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new StateMachine<State, Trigger>.ExitActionBehavior.Sync(t => { }, null));

            Assert.Equal("actionDescription", exception.ParamName);
        }

        [Fact]
        public async Task EntrySyncFromExecuteAsyncRunsActionOnlyForMatchingTrigger()
        {
            var invocations = 0;
            var behaviour = new StateMachine<State, Trigger>.EntryActionBehavior.SyncFrom<Trigger>(
                Trigger.X,
                (t, args) => invocations++,
                Invocation("Entry"));

            await behaviour.ExecuteAsync(new StateMachine<State, Trigger>.Transition(State.A, State.B, Trigger.Y), new object[0]);
            await behaviour.ExecuteAsync(new StateMachine<State, Trigger>.Transition(State.A, State.B, Trigger.X), new object[0]);

            Assert.Equal(1, invocations);
        }

        [Fact]
        public async Task EntryAsyncExecuteAsyncReturnsActionTaskAndPassesTransitionAndArgs()
        {
            var completion = new TaskCompletionSource<int>();
            var transition = new StateMachine<State, Trigger>.Transition(State.A, State.B, Trigger.X);
            var args = new object[] { "payload" };
            StateMachine<State, Trigger>.Transition capturedTransition = null;
            object[] capturedArgs = null;
            var behaviour = new StateMachine<State, Trigger>.EntryActionBehavior.Async(
                (t, a) =>
                {
                    capturedTransition = t;
                    capturedArgs = a;
                    return completion.Task;
                },
                Invocation("EntryAsync"));

            var task = behaviour.ExecuteAsync(transition, args);

            Assert.Same(completion.Task, task);
            Assert.Same(transition, capturedTransition);
            Assert.Same(args, capturedArgs);
            Assert.False(task.IsCompleted);

            completion.SetResult(0);
            await task;
        }

        [Fact]
        public void EntryAsyncExecuteThrowsWhenCalledSynchronously()
        {
            var transition = new StateMachine<State, Trigger>.Transition(State.A, State.B, Trigger.X);
            var behaviour = new StateMachine<State, Trigger>.EntryActionBehavior.Async(
                (t, a) => TaskResult.Done,
                Invocation("EntryAsync"));

            var exception = Assert.Throws<InvalidOperationException>(() => behaviour.Execute(transition, new object[0]));

            Assert.Contains("'B' state", exception.Message);
            Assert.Contains("FireAsync", exception.Message);
        }

        [Fact]
        public async Task EntryAsyncFromExecuteAsyncRunsActionOnlyForMatchingTrigger()
        {
            var invocations = 0;
            var behaviour = new StateMachine<State, Trigger>.EntryActionBehavior.AsyncFrom<Trigger>(
                Trigger.X,
                (t, args) =>
                {
                    invocations++;
                    return TaskResult.Done;
                },
                Invocation("EntryAsync"));

            var skippedTask = behaviour.ExecuteAsync(
                new StateMachine<State, Trigger>.Transition(State.A, State.B, Trigger.Y),
                new object[0]);

            Assert.True(skippedTask.IsCompleted);
            await skippedTask;
            Assert.Equal(0, invocations);

            await behaviour.ExecuteAsync(
                new StateMachine<State, Trigger>.Transition(State.A, State.B, Trigger.X),
                new object[0]);

            Assert.Equal(1, invocations);
        }

        [Fact]
        public void InternalActionSyncExecuteRunsActionWithTransitionAndArgs()
        {
            var transition = new StateMachine<State, Trigger>.Transition(State.A, State.B, Trigger.X);
            var args = new object[] { "payload" };
            StateMachine<State, Trigger>.Transition capturedTransition = null;
            object[] capturedArgs = null;
            var behaviour = new StateMachine<State, Trigger>.InternalActionBehaviour.Sync((t, a) =>
            {
                capturedTransition = t;
                capturedArgs = a;
            });

            behaviour.Execute(transition, args);

            Assert.Same(transition, capturedTransition);
            Assert.Same(args, capturedArgs);
        }

        [Fact]
        public async Task InternalActionSyncExecuteAsyncRunsActionAndCompletes()
        {
            var invoked = false;
            var behaviour = new StateMachine<State, Trigger>.InternalActionBehaviour.Sync((t, a) => invoked = true);

            var task = behaviour.ExecuteAsync(new StateMachine<State, Trigger>.Transition(State.A, State.A, Trigger.X), new object[0]);

            Assert.True(task.IsCompleted);
            await task;
            Assert.True(invoked);
        }

        [Fact]
        public async Task InternalActionAsyncExecuteAsyncReturnsActionTaskAndPassesArguments()
        {
            var transition = new StateMachine<State, Trigger>.Transition(State.A, State.A, Trigger.X);
            var args = new object[] { "payload" };
            var completion = new TaskCompletionSource<int>();
            StateMachine<State, Trigger>.Transition capturedTransition = null;
            object[] capturedArgs = null;
            var behaviour = new StateMachine<State, Trigger>.InternalActionBehaviour.Async((t, a) =>
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
        public void InternalActionAsyncExecuteThrowsWhenCalledSynchronously()
        {
            var transition = new StateMachine<State, Trigger>.Transition(State.A, State.B, Trigger.X);
            var behaviour = new StateMachine<State, Trigger>.InternalActionBehaviour.Async((t, a) => TaskResult.Done);

            var exception = Assert.Throws<InvalidOperationException>(() => behaviour.Execute(transition, new object[0]));

            Assert.Contains("'B' state", exception.Message);
            Assert.Contains("FireAsync", exception.Message);
        }

        private static InvocationInfo Invocation(string methodName)
        {
            return new InvocationInfo(methodName, null, InvocationInfo.Timing.Synchronous);
        }
    }
}
