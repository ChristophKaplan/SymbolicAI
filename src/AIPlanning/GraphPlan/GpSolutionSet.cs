using System.Collections.Generic;
using System.Linq;

namespace AIPlanning.Planning.GraphPlan {
    public class GpSolutionSet {
        private readonly List<Dictionary<int, GpLayer>> _solutions = new();
        public bool IsEmpty => _solutions.Count == 0;
        public int Count => _solutions.Count;

        public static GpSolutionSet EmptyPlan() {
            var solutionSet = new GpSolutionSet();
            solutionSet.Add(new Dictionary<int, GpLayer>());
            return solutionSet;
        }

        public void Add(Dictionary<int, GpLayer> solution)
        {
            _solutions.Add(solution);
        }
    
        // A plan is an ordered sequence of steps; the graph's absolute layer indices are an
        // extraction detail (a branch can complete above layer 0) and are not exposed.
        public IReadOnlyList<GpActionSet> GetSolution(int index) {
            return _solutions[index]
                .OrderBy(layer => layer.Key)
                .Select(layer => layer.Value.ActionSet)
                .ToList();
        }

        public override string ToString() {
            if (IsEmpty) {
                return "No solutions found!";
            }

            var result = "";

            for (var i = 0; i < _solutions.Count; i++)
            {
                result += $"Solution: {i}\n";
                var solution = GetSolution(i);
                for (var step = 0; step < solution.Count; step++)
                {
                    var actions = solution[step].Nodes.Where(actionNode => !actionNode.IsPersistenceAction);
                    var actionsAsString = string.Join("\n", actions);
                    result += $"\n STEP: {step} ACTIONS: {actionsAsString}";
                }
            }

            var output = $"Found {_solutions.Count} solutions. \n Solution: {result}";
            return output;
        }
    }
}