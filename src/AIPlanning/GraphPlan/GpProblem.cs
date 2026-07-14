using System;
using System.Collections.Generic;
using FirstOrderLogic;

namespace AIPlanning.Planning.GraphPlan {
    public class GpProblem {
        public List<ISentence> Goals { get; }
        public List<GpAction> Actions { get; }
        public List<ISentence> InitialState { get; }

        public GpProblem(List<ISentence> initialState, List<ISentence> goals, List<GpAction> actions) {
            // GraphPlan states are sets of ground literals matched exactly; a non-ground literal
            // here would silently plan against nothing, so fail loudly instead.
            ValidateGround(initialState, nameof(initialState));
            ValidateGround(goals, nameof(goals));
            InitialState = initialState;
            Goals = goals;
            Actions = actions;
        }

        private static void ValidateGround(List<ISentence> literals, string paramName) {
            foreach (var literal in literals) {
                if (!literal.IsGround()) {
                    throw new ArgumentException(
                        $"GraphPlan handles ground literals only, but '{literal}' contains variables", paramName);
                }
            }
        }

        public GpSolution Solve() {
            return GraphPlanAlgo.Run(this);
        }
    }
}
