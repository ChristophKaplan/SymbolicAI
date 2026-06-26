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

        public GpSolution ExtractSolution(int levelIndex, NoGoods noGoods) {
            var lastState = _layers[levelIndex].BeliefState;
            lastState.IsConflictFreeStateReachable(_problem.Goals, out var currentState);

            var solutions = new GpSolution();
            if (currentState == null) {
                return solutions;
            }

            FindSolutions(levelIndex, currentState, noGoods, new Dictionary<int, GpLayer>(), solutions);
            return solutions;
        }

        private bool FindSolutions(int levelIndex, GpBeliefState curBeliefState, NoGoods noGoods, Dictionary<int, GpLayer> outcome, GpSolution solutions) {
            // The "no goal literals at this level" branch was reached either because a
            // recursive call drilled below level 0 or because the goals were empty.
            // Treat it as a base case with success.
            if (curBeliefState.GetNodes.Count == 0) {
                if (levelIndex < 0) {
                    var emptySolution = outcome.Reverse().ToDictionary(pair => pair.Key, pair => pair.Value);
                    solutions.Add(emptySolution);
                    return true;
                }
                return false;
            }

            // B-fix: prune already-recorded nogoods.
            if (noGoods.Contains(levelIndex, curBeliefState)) {
                return false;
            }

            var nextLevelIndex = levelIndex - 1;
            var anyBranchSucceeded = false;

            // Empty stream (no conflict-free action set supports this goal state) falls through to
            // the !anyBranchSucceeded block below, which records the nogood — same as before.
            foreach (var possibleConditionalActions in curBeliefState.GetPossibleConflictFreeActionSets()) {
                var preConditionalState = possibleConditionalActions.GetJointPreconditionsIfConflictFree();

                // Skip action sets whose joint preconditions are mutex at the previous
                // literal level (infeasible) OR are empty (nothing left to recurse on).
                if (preConditionalState == null || preConditionalState.GetNodes.Count == 0) {
                    continue;
                }

                var possibleLayer = new GpLayer(nextLevelIndex, preConditionalState, possibleConditionalActions);
                var outcomeBranch = new Dictionary<int, GpLayer>(outcome) { { nextLevelIndex, possibleLayer } };

                if (nextLevelIndex == 0) {
                    var solution = outcomeBranch.Reverse().ToDictionary(pair => pair.Key, pair => pair.Value);
                    solutions.Add(solution);
                    anyBranchSucceeded = true;
                    continue;
                }

                if (FindSolutions(nextLevelIndex, preConditionalState, noGoods, outcomeBranch, solutions)) {
                    anyBranchSucceeded = true;
                }
            }

            // B-fix: only mark as nogood when ALL branches from this state failed.
            if (!anyBranchSucceeded) {
                noGoods.Add(levelIndex, curBeliefState);
            }

            return anyBranchSucceeded;
        }

        // True iff the planning graph has reached a fixed point at this level.
        // A single "layer N == layer N-1" is not enough because the unification-driven
        // OperatorGraph can produce a transient identity between two levels and then
        // grow again — we therefore require TWO consecutive equalities.
        public bool Stable(int levelIndex) {
            if (levelIndex < 2 || _layers.Count < 3) {
                return false;
            }

            var n0 = _layers[levelIndex].BeliefState;
            var n1 = _layers[levelIndex - 1].BeliefState;
            var n2 = _layers[levelIndex - 2].BeliefState;
            return n0.EqualStateLiterals(n1) && n1.EqualStateLiterals(n2);
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
