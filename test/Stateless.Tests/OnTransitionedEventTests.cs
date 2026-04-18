using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using static Stateless.StateMachine<object, object>;

namespace Stateless.Tests
{
    public class OnTransitionedEventTests : IDisposable
    {
        private readonly OnTransitionedEvent subject;

        private event Action<Transition> onTransitioned;
        private List<Func<Transition, Task>> onTransitionedAsync;

        private Action<Transition> registeredAction;
        private Func<Transition, Task> registeredAsyncAction;

        private int transitionCounter = 0;

        public OnTransitionedEventTests()
        {
            subject = new OnTransitionedEvent();

            registeredAction = (Transition _) =>
            {
                Interlocked.Increment(ref transitionCounter);
            };

            registeredAsyncAction = (Transition _) =>
            {
                Interlocked.Increment(ref transitionCounter);
                return Task.FromResult(0);
            };
        }

        public void Dispose()
        {
            subject.UnregisterAll();
        }

        private void InstantiateTransitionReferences()
        {
            FieldInfo[] subjectFields = subject.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic);

            onTransitioned = subjectFields[0].GetValue(subject) as Action<Transition>;
            onTransitionedAsync = subjectFields[1].GetValue(subject) as List<Func<Transition, Task>>;
        }

        [Fact]
        public void Register_ShouldOnlyRegisterActionOnce_WhenCalled()
        {
            subject.Register(registeredAction);
            subject.Register(registeredAction);

            InstantiateTransitionReferences();

            Assert.Single(onTransitioned.GetInvocationList() ?? Array.Empty<Delegate>());
        }

        [Fact]
        public void Register_ShouldRegisterDistinctActions_WhenCalled()
        {
            subject.Register(registeredAction);
            subject.Register((Transition _) => { });

            InstantiateTransitionReferences();

            Assert.Equal(2, onTransitioned.GetInvocationList().Length);
        }

        [Fact]
        public void Register_ShouldOnlyRegisterFuncOnce_WhenCalled()
        {
            subject.Register(registeredAsyncAction);
            subject.Register(registeredAsyncAction);

            InstantiateTransitionReferences();

            Assert.Single(onTransitionedAsync);
        }

        [Fact]
        public void Register_ShouldRegisterDistinctFuncActions_WhenCalled()
        {
            subject.Register(registeredAsyncAction);
            subject.Register((Transition _) => Task.FromResult(0));

            InstantiateTransitionReferences();

            Assert.Equal(2, onTransitionedAsync.Count);
        }

        [Fact]
        public void Unregister_ShouldNotInvokeActionTwice_WhenCalled()
        {
            subject.Register(registeredAction);
            subject.Invoke(new Transition(null, null, null));

            Assert.Equal(1, transitionCounter);

            subject.Unregister(registeredAction);
            subject.Invoke(new Transition(null, null, null));

            Assert.Equal(1, transitionCounter);
        }

        [Fact]
        public async Task Unregister_ShouldNotInvokeAsyncActionTwice_WhenCalled()
        {
            subject.Register(registeredAsyncAction);
            await subject.InvokeAsync(new Transition(null, null, null), true);

            Assert.Equal(1, transitionCounter);

            subject.Unregister(registeredAsyncAction);
            await subject.InvokeAsync(new Transition(null, null, null), true);

            Assert.Equal(1, transitionCounter);
        }

        [Fact]
        public void UnregisterAll_ShouldRemoveSyncAndAsyncActions_WhenCalled()
        {
            subject.Register(registeredAction);
            subject.Register(registeredAsyncAction);

            InstantiateTransitionReferences();

            Assert.Equal(1, onTransitioned.GetInvocationList().Length);
            Assert.Single(onTransitionedAsync);

            subject.UnregisterAll();

            InstantiateTransitionReferences();

            Assert.Null(onTransitioned);
            Assert.Empty(onTransitionedAsync);
        }

        [Fact]
        public void Invoke_ShouldCallRegisteredAction_WhenCalled()
        {
            subject.Register(registeredAction);
            subject.Invoke(new Transition(null, null, null));
            subject.Invoke(new Transition(null, null, null));

            Assert.Equal(2, transitionCounter);
        }

        [Fact]
        public void Invoke_ShouldThrowInvalidOperationException_WhenAsyncActionRegistered()
        {
            subject.Register(registeredAction);
            subject.Register(registeredAsyncAction);

            Assert.Throws<InvalidOperationException>(() => subject.Invoke(new Transition(null, null, null)));
        }

        [Fact]
        public async Task InvokeAsync_ShouldCallSyncAction_WhenRegistered()
        {
            subject.Register(registeredAction);
            subject.Register(registeredAsyncAction);

            await subject.InvokeAsync(new Transition(null, null, null), true);

            Assert.Equal(2, transitionCounter);
        }

        [Fact]
        public async Task InvokeAsync_ShouldNotCallSyncAction_WhenNotRegistered()
        {
            subject.Register(registeredAsyncAction);

            await subject.InvokeAsync(new Transition(null, null, null), true);

            Assert.Equal(1, transitionCounter);
        }

        [Fact]
        public async Task InvokeAsync_ShouldNotCompleteUntilAsyncActionCompletes()
        {
            var callbackStarted = new TaskCompletionSource<int>();
            var releaseCallback = new TaskCompletionSource<int>();

            subject.Register(async _ =>
            {
                callbackStarted.SetResult(0);
                await releaseCallback.Task;
                Interlocked.Increment(ref transitionCounter);
            });

            var invokeTask = subject.InvokeAsync(new Transition(null, null, null), true);

            await callbackStarted.Task;

            Assert.False(invokeTask.IsCompleted);
            Assert.Equal(0, transitionCounter);

            releaseCallback.SetResult(0);
            await invokeTask;

            Assert.Equal(1, transitionCounter);
        }

        [Fact]
        public async Task InvokeAsync_ShouldNotRetainSynchronizationContext_WhenRequestedFalse()
        {
            var previousContext = SynchronizationContext.Current;
            var customContext = new SynchronizationContext();
            SynchronizationContext firstCallbackContext = null;
            SynchronizationContext secondCallbackContext = null;
            var releaseFirst = new TaskCompletionSource<int>();

            subject.Register(async _ =>
            {
                firstCallbackContext = SynchronizationContext.Current;
                await releaseFirst.Task.ConfigureAwait(false);
            });
            subject.Register(_ =>
            {
                secondCallbackContext = SynchronizationContext.Current;
                return Task.FromResult(0);
            });

            SynchronizationContext.SetSynchronizationContext(customContext);
            var invokeTask = subject.InvokeAsync(new Transition(null, null, null), false);
            SynchronizationContext.SetSynchronizationContext(previousContext);

            releaseFirst.SetResult(0);
            await invokeTask.ConfigureAwait(false);

            Assert.Same(customContext, firstCallbackContext);
            Assert.NotSame(customContext, secondCallbackContext);
        }

        [Fact]
        public async Task InvokeAsync_ShouldAwaitAsyncActionsSequentially_WhenMultipleRegistered()
        {
            var order = new List<string>();
            var firstStarted = new TaskCompletionSource<int>();
            var releaseFirst = new TaskCompletionSource<int>();

            subject.Register((Transition _) => order.Add("sync"));
            subject.Register(async _ =>
            {
                order.Add("async1-start");
                firstStarted.SetResult(0);
                await releaseFirst.Task;
                order.Add("async1-end");
            });
            subject.Register(_ =>
            {
                order.Add("async2");
                return Task.FromResult(0);
            });

            var invokeTask = subject.InvokeAsync(new Transition(null, null, null), true);
            await firstStarted.Task;

            Assert.False(invokeTask.IsCompleted);
            Assert.Equal(new[] { "sync", "async1-start" }, order);

            releaseFirst.SetResult(0);
            await invokeTask;

            Assert.Equal(new[] { "sync", "async1-start", "async1-end", "async2" }, order);
        }

        [Fact]
        public async Task InvokeAsync_ShouldPropagateAsyncActionExceptionAndSkipLaterAsyncActions()
        {
            var laterInvoked = false;
            var expected = new InvalidOperationException("boom");
            subject.Register(_ =>
            {
                var task = new TaskCompletionSource<int>();
                task.SetException(expected);
                return task.Task;
            });
            subject.Register(_ =>
            {
                laterInvoked = true;
                return Task.FromResult(0);
            });

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                subject.InvokeAsync(new Transition(null, null, null), true));

            Assert.Equal(expected.Message, exception.Message);
            Assert.False(laterInvoked);
        }
    }
}
