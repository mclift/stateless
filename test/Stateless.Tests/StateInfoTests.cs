using System;
using Stateless.Reflection;
using Xunit;

namespace Stateless.Tests
{
    public class StateInfoTests
    {
        [Fact]
        public void StateInfo_CreateStateInfo_ThrowsArgumentExceptionForNullStateRepresentation()
        {
            var exception = Assert.Throws<ArgumentException>(() =>
                StateInfo.CreateStateInfo<State, Trigger>(null));

            Assert.Contains("stateRepresentation", exception.Message);
        }

        [Fact]
        public void StateInfo_ToString_ReturnsNullStringForNullUnderlyingState()
        {
            var stateInfo = StateInfo.CreateStateInfo(new StateMachine<string, string>.StateRepresentation(null));

            Assert.Null(stateInfo.UnderlyingState);
            Assert.Equal(SpecialConstants.NullString, stateInfo.ToString());
        }

        [Fact]
        public void StateInfo_AddRelationships_ThrowsArgumentNullExceptionForNullLookupState()
        {
            var representation = new StateMachine<State, Trigger>.StateRepresentation(State.A);
            var stateInfo = StateInfo.CreateStateInfo(representation);

            var exception = Assert.Throws<ArgumentNullException>(() =>
                StateInfo.AddRelationships(stateInfo, representation, null));

            Assert.Equal("lookupState", exception.ParamName);
        }

        /// <summary>
        /// For StateInfo, Substates, FixedTransitions and DynamicTransitions are only initialised by a call to AddRelationships.
        /// However, for StateMachineInfo.InitialState, this never happens. Therefore StateMachineInfo.InitialState.Transitions
        /// throws a System.ArgumentNullException.
        /// </summary>
        [Fact]
        public void StateInfo_transitions_should_default_to_empty()
        {
            // ARRANGE
            var stateInfo = StateInfo.CreateStateInfo(new StateMachine<State, Trigger>.StateRepresentation(State.A));

            // ACT
            var stateInfoTransitions = stateInfo.Transitions;

            // ASSERT
            Assert.Null(stateInfoTransitions);
        }
    }
}
