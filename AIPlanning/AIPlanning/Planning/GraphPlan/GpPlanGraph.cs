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

        // stopAtFirst: return as soon as ONE complete plan is found instead of enumerating
        // every supporter combination — the exhaustive walk is exponential in the number of
        // interchangeable supporters, and callers that only execute one plan don't need it.
        // Note the early exit also skips nogood recording for branches never explored; that
        // only matters on success, where the nogood table is discarded anyway.
        public GpSolution ExtractSolution(int levelIndex, NoGoods noGoods, bool stopAtFirst = false) {
            var lastState = _layers[levelIndex].BeliefState;
            var solutions = new GpSolution();

            // When the goals are not jointly conflict-free at this level, the out-state is only
            // the mutex-stripped SUBSET of the goals — extracting from it would yield a plan
            // that achieves part of the goals. No plan exists at this level.
            if (!lastState.IsConflictFreeStateReachable(_problem.Goals, out var currentState)) {
                return solutions;
            }

            FindSolutions(levelIndex, currentState!, noGoods, new Dictionary<int, GpLayer>(), solutions, stopAtFirst);
            return solutions;
        }

        private bool FindSolutions(int levelIndex, GpBeliefState curBeliefState, NoGoods noGoods, Dictionary<int, GpLayer> outcome, GpSolution solutions, bool stopAtFirst) {
            // Extraction entered at layer 0: the initial state itself supplies the (already
            // conflict-free) goals, so the empty plan succeeds. Only top-level calls can land
            // here — the recursion below handles nextLevelIndex == 0 before recursing.
            if (levelIndex == 0) {
                solutions.Add(outcome);
                return true;
            }

            // No goal literals to support at this level: the empty state is trivially
            // satisfied, so the branch is complete. Only top-level calls with an empty goal
            // set land here — the recursion below handles empty precondition states itself.
            if (curBeliefState.GetNodes.Count == 0) {
                solutions.Add(outcome);
                return true;
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
                // literal level (infeasible).
                if (preConditionalState == null) {
                    continue;
                }

                var possibleLayer = new GpLayer(nextLevelIndex, preConditionalState, possibleConditionalActions);
                var outcomeBranch = new Dictionary<int, GpLayer>(outcome) { { nextLevelIndex, possibleLayer } };

                // Empty joint preconditions (all chosen actions are precondition-less) means the
                // branch needs nothing from the levels below — it is complete, not a dead end.
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
        // The comparison must include the mutex relations: the literal set typically
        // stabilises several levels before the mutexes finish relaxing, and declaring
        // level-off on literals alone reports solvable problems as unsolvable.
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
