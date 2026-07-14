using System.Collections.Generic;
using System.Linq;
using FirstOrderLogic;

namespace AIPlanning.Planning.GraphPlan {
    public class GpPlanGraph {
        private readonly OperatorGraph _operatorGraph;
        private readonly Dictionary<int, GpLayer> _layers = new();
        private readonly Dictionary<ISentence, GpAction> _persistCache = new();
        private readonly GpProblem _problem;

        public GpPlanGraph(GpProblem problem) {
            _problem = problem;
            _operatorGraph = new OperatorGraph(problem);

            var initialLayer = new GpLayer(0);
            foreach (var sentence in problem.InitialState) {
                initialLayer.TryAdd(new GpLiteralNode(sentence));
            }

            initialLayer.BeliefState.GetNodes.CheckMutexRelations();
            _layers.Add(0, initialLayer);
        }

        public bool StateNotMutex(int i, List<ISentence> sentences) {
            return _layers[i].BeliefState.IsConflictFreeStateReachable(sentences, out _);
        }

        // stopAtFirst: return on the first complete plan instead of the exhaustive walk, which is
        // exponential in the number of interchangeable supporters. The early exit skips nogood
        // recording for unexplored branches — harmless, as that only happens on success, where
        // the nogood table is discarded anyway.
        public GpSolution ExtractSolution(int levelIndex, NoGoods noGoods, bool stopAtFirst = false) {
            var lastState = _layers[levelIndex].BeliefState;
            var solutions = new GpSolution();

            // Goals not jointly conflict-free here: the out-state is only the mutex-stripped
            // SUBSET of the goals — extracting from it would yield a partial plan.
            if (!lastState.IsConflictFreeStateReachable(_problem.Goals, out var currentState)) {
                return solutions;
            }

            FindSolutions(levelIndex, currentState!, noGoods, new Dictionary<int, GpLayer>(), solutions, stopAtFirst);
            return solutions;
        }

        private bool FindSolutions(int levelIndex, GpBeliefState curBeliefState, NoGoods noGoods, Dictionary<int, GpLayer> outcome, GpSolution solutions, bool stopAtFirst) {
            if (levelIndex == 0) {
                solutions.Add(outcome);
                return true;
            }

            if (curBeliefState.GetNodes.Count == 0) {
                solutions.Add(outcome);
                return true;
            }

            if (noGoods.Contains(levelIndex, curBeliefState)) {
                return false;
            }

            var nextLevelIndex = levelIndex - 1;
            var anyBranchSucceeded = false;

            foreach (var possibleConditionalActions in curBeliefState.GetPossibleConflictFreeActionSets()) {
                var preConditionalState = possibleConditionalActions.GetJointPreconditionsIfConflictFree();
                if (preConditionalState == null) {
                    continue;
                }

                var possibleLayer = new GpLayer(nextLevelIndex, preConditionalState, possibleConditionalActions);
                var outcomeBranch = new Dictionary<int, GpLayer>(outcome) { { nextLevelIndex, possibleLayer } };

                // Empty joint preconditions mean the branch needs nothing from the levels below —
                // it is complete, not a dead end.
                if (nextLevelIndex == 0 || preConditionalState.GetNodes.Count == 0) {
                    solutions.Add(outcomeBranch);
                    anyBranchSucceeded = true;
                    if (stopAtFirst) {
                        return true;
                    }
                    continue;
                }

                if (FindSolutions(nextLevelIndex, preConditionalState, noGoods, outcomeBranch, solutions, stopAtFirst)) {
                    anyBranchSucceeded = true;
                    if (stopAtFirst) {
                        return true;
                    }
                }
            }

            if (!anyBranchSucceeded) {
                noGoods.Add(levelIndex, curBeliefState);
            }

            return anyBranchSucceeded;
        }

        // A single "layer N == layer N-1" is not enough: the unification-driven OperatorGraph can
        // produce a transient identity and then grow again, hence TWO consecutive equalities.
        // The comparison must include mutex relations — literals stabilise before mutexes finish
        // relaxing, and declaring level-off on literals alone reports solvable problems unsolvable.
        public bool Stable(int levelIndex) {
            if (levelIndex < 2 || _layers.Count < 3) {
                return false;
            }

            var n0 = _layers[levelIndex].BeliefState;
            var n1 = _layers[levelIndex - 1].BeliefState;
            var n2 = _layers[levelIndex - 2].BeliefState;
            return n0.EqualState(n1) && n1.EqualState(n2);
        }

        public void ExpandGraph() {
            var curLayer = _layers.Last().Value;

            var usableActions = curLayer.GetUsableActions(_operatorGraph);

            curLayer.ExpandActions(usableActions, _persistCache);
            curLayer.ActionSet.GetNodes.CheckMutexRelations();
            var nextLayer = curLayer.ExpandLayer();
            _layers.Add(nextLayer.Level, nextLayer);
        }

        public override string ToString() {
            return _layers.Aggregate("", (current, layer) => current + layer.Value);
        }
    }
}
