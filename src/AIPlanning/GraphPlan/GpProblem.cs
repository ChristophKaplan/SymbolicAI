using System.Collections.Generic;
using FirstOrderLogic;

namespace AIPlanning.Planning.GraphPlan {
    public class GpProblem {
        public List<ISentence> Goals { get; }
        public List<GpAction> Actions { get; }
        public List<ISentence> InitialState { get; }

        public GpProblem(List<ISentence> initialState, List<ISentence> goals, List<GpAction> actions) {
            InitialState = initialState;
            Goals = goals;
            Actions = actions;
        }

        public GpSolution Solve() {
            return new GraphPlanAlgo().Run(this);
        }
    }
}
