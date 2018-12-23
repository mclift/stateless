using System.Collections.Generic;

namespace Stateless.Graph
{
    /// <summary>
    /// Used to keep track of a state that has substates
    /// </summary>
    public class SuperState : State
    {
        /// <summary>
        /// List of states that are a substate of this state
        /// </summary>
        public List<State> SubStates { get; } = new List<State>();

        /// <summary>
        /// Gets or sets the last non-superstate child of this superstate.
        /// </summary>
        public State LastChild { get; set; }

        internal SuperState(Reflection.StateInfo stateInfo)
            : base(stateInfo)
        {

        }
    }
}
