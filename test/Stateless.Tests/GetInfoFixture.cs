using System.Threading.Tasks;
using Xunit;

namespace Stateless.Tests
{
    public class GetInfoFixture
    {
        [Fact]
        public void GetInfo_should_return_Entry_action_without_trigger_name()
        {
            // ARRANGE
            var sm = new StateMachine<State, Trigger>(State.A);
            sm.Configure(State.B)
                .OnEntry(() => { });

            // ACT
            var stateMachineInfo = sm.GetInfo();

            // ASSERT
            var stateInfo = Assert.Single(stateMachineInfo.States);
            var entryActionInfo = Assert.Single(stateInfo.EntryActions);
            Assert.Null(entryActionInfo.FromTrigger);
        }

        [Fact]
        public void GetInfo_should_return_Entry_action_with_trigger_name()
        {
            // ARRANGE
            var sm = new StateMachine<State, Trigger>(State.A);
            sm.Configure(State.B)
                .OnEntryFrom(Trigger.X, () => { });
        
            // ACT
            var stateMachineInfo = sm.GetInfo();
        
            // ASSERT
            var stateInfo = Assert.Single(stateMachineInfo.States);
            var entryActionInfo = Assert.Single(stateInfo.EntryActions);
            Assert.Equal(Trigger.X.ToString(), entryActionInfo.FromTrigger);
        }

        [Fact]
        public void GetInfo_should_return_raw_Entry_action_trigger_name()
        {
            // ARRANGE
            var trigger = new TriggerLabel("Trigger \"X\"\n[raw]");
            var sm = new StateMachine<State, TriggerLabel>(State.A);
            sm.Configure(State.B)
                .OnEntryFrom(trigger, () => { });

            // ACT
            var stateMachineInfo = sm.GetInfo();

            // ASSERT
            var stateInfo = Assert.Single(stateMachineInfo.States);
            var entryActionInfo = Assert.Single(stateInfo.EntryActions);
            Assert.Equal(trigger.ToString(), entryActionInfo.FromTrigger);
        }
    
        [Fact]
        public void GetInfo_should_return_async_Entry_action_with_trigger_name()
        {
            // ARRANGE
            var sm = new StateMachine<State, Trigger>(State.A);
            sm.Configure(State.B)
                .OnEntryFromAsync(Trigger.X, () => Task.CompletedTask);
        
            // ACT
            var stateMachineInfo = sm.GetInfo();
        
            // ASSERT
            var stateInfo = Assert.Single(stateMachineInfo.States);
            var entryActionInfo = Assert.Single(stateInfo.EntryActions);
            Assert.Equal(Trigger.X.ToString(), entryActionInfo.FromTrigger);
        }

        [Fact]
        public void GetInfo_should_return_raw_async_Entry_action_trigger_name()
        {
            // ARRANGE
            var trigger = new TriggerLabel("Async trigger \"Y\"\n[raw]");
            var sm = new StateMachine<State, TriggerLabel>(State.A);
            sm.Configure(State.B)
                .OnEntryFromAsync(trigger, () => Task.CompletedTask);

            // ACT
            var stateMachineInfo = sm.GetInfo();

            // ASSERT
            var stateInfo = Assert.Single(stateMachineInfo.States);
            var entryActionInfo = Assert.Single(stateInfo.EntryActions);
            Assert.Equal(trigger.ToString(), entryActionInfo.FromTrigger);
        }

        private sealed class TriggerLabel
        {
            private readonly string _label;

            public TriggerLabel(string label)
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
