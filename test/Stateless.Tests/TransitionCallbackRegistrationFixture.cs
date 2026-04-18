using System.Threading.Tasks;
using Xunit;

namespace Stateless.Tests
{
    public class TransitionCallbackRegistrationFixture
    {
        [Fact]
        public void OnTransitionedUnregister_PreventsFurtherSyncCallbacks()
        {
            var sm = CreateTwoStateMachine();
            var callbackCount = 0;

            void Callback(StateMachine<State, Trigger>.Transition transition) => callbackCount++;

            sm.OnTransitioned(Callback);

            sm.Fire(Trigger.X);
            Assert.Equal(1, callbackCount);

            sm.OnTransitionedUnregister(Callback);

            sm.Fire(Trigger.Y);
            Assert.Equal(1, callbackCount);
        }

        [Fact]
        public void OnTransitionCompletedUnregister_PreventsFurtherSyncCallbacks()
        {
            var sm = CreateTwoStateMachine();
            var callbackCount = 0;

            void Callback(StateMachine<State, Trigger>.Transition transition) => callbackCount++;

            sm.OnTransitionCompleted(Callback);

            sm.Fire(Trigger.X);
            Assert.Equal(1, callbackCount);

            sm.OnTransitionCompletedUnregister(Callback);

            sm.Fire(Trigger.Y);
            Assert.Equal(1, callbackCount);
        }

        [Fact]
        public async Task OnTransitionedAsyncUnregister_PreventsFurtherAsyncCallbacks()
        {
            var sm = CreateTwoStateMachine();
            var callbackCount = 0;

            Task Callback(StateMachine<State, Trigger>.Transition transition)
            {
                callbackCount++;
                return Task.CompletedTask;
            }

            sm.OnTransitionedAsync(Callback);

            await sm.FireAsync(Trigger.X);
            Assert.Equal(1, callbackCount);

            sm.OnTransitionedAsyncUnregister(Callback);

            await sm.FireAsync(Trigger.Y);
            Assert.Equal(1, callbackCount);
        }

        [Fact]
        public async Task OnTransitionCompletedAsyncUnregister_PreventsFurtherAsyncCallbacks()
        {
            var sm = CreateTwoStateMachine();
            var callbackCount = 0;

            Task Callback(StateMachine<State, Trigger>.Transition transition)
            {
                callbackCount++;
                return Task.CompletedTask;
            }

            sm.OnTransitionCompletedAsync(Callback);

            await sm.FireAsync(Trigger.X);
            Assert.Equal(1, callbackCount);

            sm.OnTransitionCompletedAsyncUnregister(Callback);

            await sm.FireAsync(Trigger.Y);
            Assert.Equal(1, callbackCount);
        }

        [Fact]
        public async Task UnregisterAllCallbacks_RemovesSyncAndAsyncTransitionCallbacks()
        {
            var sm = CreateTwoStateMachine();
            var transitionedCount = 0;
            var completedCount = 0;

            void OnTransitioned(StateMachine<State, Trigger>.Transition transition) => transitionedCount++;
            void OnCompleted(StateMachine<State, Trigger>.Transition transition) => completedCount++;

            Task OnTransitionedAsync(StateMachine<State, Trigger>.Transition transition)
            {
                transitionedCount++;
                return Task.CompletedTask;
            }

            Task OnCompletedAsync(StateMachine<State, Trigger>.Transition transition)
            {
                completedCount++;
                return Task.CompletedTask;
            }

            sm.OnTransitioned(OnTransitioned);
            sm.OnTransitionCompleted(OnCompleted);
            sm.OnTransitionedAsync(OnTransitionedAsync);
            sm.OnTransitionCompletedAsync(OnCompletedAsync);

            await sm.FireAsync(Trigger.X);

            Assert.Equal(2, transitionedCount);
            Assert.Equal(2, completedCount);

            sm.UnregisterAllCallbacks();

            await sm.FireAsync(Trigger.Y);

            Assert.Equal(2, transitionedCount);
            Assert.Equal(2, completedCount);
        }

        private static StateMachine<State, Trigger> CreateTwoStateMachine()
        {
            var sm = new StateMachine<State, Trigger>(State.A);

            sm.Configure(State.A)
                .Permit(Trigger.X, State.B);

            sm.Configure(State.B)
                .Permit(Trigger.Y, State.A);

            return sm;
        }
    }
}
