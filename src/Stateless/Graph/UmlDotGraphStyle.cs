using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Stateless.Graph
{
    /// <summary>
    /// Generate DOT graphs in basic UML style
    /// </summary>
    public class UmlDotGraphStyle : IGraphStyle
    {
        /// <summary>Get the text that starts a new graph</summary>
        /// <returns></returns>
        override internal string GetPrefix()
        {
            return "digraph {\n"
                      + "compound=true;\n"
                      + "node [shape=Mrecord]\n"
                      + "rankdir=\"LR\"\n";
        }

        internal override string FormatOneCluster(SuperState stateInfo)
        {
            string stateRepresentationString = "";
            var sourceName = stateInfo.StateName;

            StringBuilder label = new StringBuilder(sourceName);

            if ((stateInfo.EntryActions.Count > 0) || (stateInfo.ExitActions.Count > 0))
            {
                label.Append("\\n----------");
                label.Append(string.Concat(stateInfo.EntryActions.Select(act => "\\nentry / " + act)));
                label.Append(string.Concat(stateInfo.ExitActions.Select(act => "\\nexit / " + act)));
            }

            stateRepresentationString = "\n"
                + $"subgraph {GetClusterName(stateInfo)}" + "\n"
                + "\t{" + "\n"
                + $"\tlabel = \"{label.ToString()}\"" + "\n";

            foreach (var subState in stateInfo.SubStates)
            {
                stateRepresentationString += FormatState(subState);
            }

            stateRepresentationString += "}\n";

            return stateRepresentationString;
        }

        private static string GetClusterName(State state)
        {
            return $"cluster{state.NodeName}";
        }

        /// <summary>
        /// Generate the text for the state or superstate.
        /// </summary>
        /// <param name="state">The state or superstate.</param>
        /// <returns></returns>
        public override string FormatState(State state)
        {
            if (state is SuperState superState)
            {
                return FormatOneCluster(superState);
            }
            else
            {
                return FormatOneState(state);
            }
        }

        /// <summary>
        /// Generate the text for a single state
        /// </summary>
        /// <param name="state">The state to generate text for</param>
        /// <returns></returns>
        override internal string FormatOneState(State state)
        {
            if ((state.EntryActions.Count == 0) && (state.ExitActions.Count == 0))
                return state.StateName + " [label=\"" + state.StateName + "\"];\n";

            string f = state.StateName + " [label=\"" + state.StateName + "|";

            List<string> es = new List<string>();
            es.AddRange(state.EntryActions.Select(act => "entry / " + act));
            es.AddRange(state.ExitActions.Select(act => "exit / " + act));

            f += String.Join("\\n", es);

            f += "\"];\n";

            return f;
        }

        /// <summary>
        /// Generate text for a single transition
        /// </summary>
        /// <param name="sourceNode"></param>
        /// <param name="trigger"></param>
        /// <param name="actions"></param>
        /// <param name="destinationNode"></param>
        /// <param name="guards"></param>
        /// <returns></returns>
        override internal string FormatOneTransition(State sourceNode, string trigger, IEnumerable<string> actions,
            State destinationNode, IEnumerable<string> guards)
        {
            string label = trigger ?? "";

            if (actions?.Count() > 0)
                label += " / " + string.Join(", ", actions);

            if (guards.Any())
            {
                foreach (var info in guards)
                {
                    if (label.Length > 0)
                        label += " ";
                    label += "[" + info + "]";
                }
            }

            var sourceNodeName = GetConnectedNodeName(sourceNode, out var tailNodeName);
            var destinationNodeName = GetConnectedNodeName(destinationNode, out var headNodeName);

            return FormatOneLine(sourceNodeName, destinationNodeName, label, tailNodeName, headNodeName);
        }

        /// <summary>
        /// Gets a node name that can be used to attach a transition; outputs the name of the cluster
        /// (if any) to which the transition should be connected in the DOT graph.
        /// </summary>
        /// <remarks>
        /// This methods is used to avoid parent states from being drawn as separate entities in generated
        /// state diagrams when one or more transitions have the parent state as an endpoint. This method
        /// checks whether the connected state is a parent, and returns one of its immediate child state
        /// names if so; additionally, it outputs the DOT graph cluster name that the drawn transition
        /// should be attached to by using the "ltail" or "lhead" attribute. If the state is not a parent,
        /// the <code>NodeName</code> is returned, and the cluster name is output as <code>null</code>.
        /// </remarks>
        /// <param name="state">The endpoint state.</param>
        /// <param name="connectedClusterName">When connecting a parent state, the state's cluster name; otherwise, null.</param>
        /// <returns>When connecting a parent state, one of the immediate child state names; otherwise, the state name.</returns>
        private static string GetConnectedNodeName(State state, out string connectedClusterName)
        {
            string nodeName;
            connectedClusterName = null;
            if (state is SuperState superState)
            {
                nodeName = superState.LastChild?.NodeName ?? superState.NodeName;
                connectedClusterName = GetClusterName(superState);
            }
            else
            {
                nodeName = state.NodeName;
            }

            return nodeName;
        }

        /// <summary>
        /// Generate the text for a single decision node
        /// </summary>
        /// <param name="nodeName">Name of the node</param>
        /// <param name="label">Label for the node</param>
        /// <returns></returns>
        override internal string FormatOneDecisionNode(string nodeName, string label)
        {
            return nodeName + " [shape = \"diamond\", label = \"" + label + "\"];\n";
        }

        internal string FormatOneLine(string fromNodeName, string toNodeName, string label, string tailNodeName = null, string headNodeName = null)
        {
            var tailExpression = tailNodeName != null ? $", ltail={tailNodeName}" : "";
            var headExpression = headNodeName != null ? $", lhead={headNodeName}" : "";

            return $"{fromNodeName} -> {toNodeName} [style=\"solid\", label=\"{label}\"{tailExpression}{headExpression}];";
        }
    }
}
