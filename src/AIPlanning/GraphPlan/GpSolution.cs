using System;
using System.Collections.Generic;
using System.Linq;

namespace AIPlanning.Planning.GraphPlan {
    public class GpSolution {
        private readonly List<Dictionary<int, GpLayer>> _solutions = new();
        public bool IsEmpty => _solutions.Count == 0;
        public int Count => _solutions.Count;

        public static GpSolution EmptyPlan() {
            var s = new GpSolution();
            s.Add(new Dictionary<int, GpLayer>());
            return s;
        }

        public void Add(Dictionary<int, GpLayer> solution)
        {
            _solutions.Add(solution);
        }
    
        public SortedDictionary<int, GpActionSet> GetSolution(int index) {
            if (index < 0 || index >= _solutions.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index,
                    $"Solution index must be in [0, {_solutions.Count - 1}]");
            }

            // SortedDictionary so step order is a property of the type, not of insertion order.
            var solution = new SortedDictionary<int, GpActionSet>();
            foreach (var solutionLayer in _solutions[index]) {
                solution.Add(solutionLayer.Key, solutionLayer.Value.ActionSet);
            }

            return solution;
        }
    
        public override string ToString() {
            if (IsEmpty) return "No solutions found!";
        
            var result = "";

            for (var i = 0; i < _solutions.Count; i++)
            {
                result += $"Solution: {i}\n";
                var solution = GetSolution(i);
                foreach (var step in solution)
                {
                    var actions = step.Value.GetActionNodes.Where(actionNode => !actionNode.IsPersistenceAction);
                    var actionsAsString = string.Join("\n", actions);
                    result += $"\n STEP: {step.Key} ACTIONS: {actionsAsString}";
                }
            }

            var output = $"Found {_solutions.Count} solutions. \n Solution: {result}";
            return output;
        }
    }
}