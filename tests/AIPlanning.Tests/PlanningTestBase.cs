using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AIPlanning.Planning.GraphPlan;
using FirstOrderLogic;
using NUnit.Framework;

namespace AIPlanningTests {
    // NUnit's [Timeout] cannot abort a hung test on .NET Core (no Thread.Abort), so
    // runaway-suspect solves run in a Task and must finish within a hard wait bound —
    // generous, because wall-clock assertions flake on loaded CI machines.
    public abstract class PlanningTestBase {
        protected static readonly GpActionFactory Factory = new();
        protected static readonly TimeSpan SolveBound = TimeSpan.FromSeconds(30);

        protected static ISentence L(string s) =>
            Factory.ParseSentences(new List<string> { s }).Single();

        protected static GpSolutionSet SolveWithGuard(GpProblem problem, string label = "Solve()") {
            var task = Task.Run(problem.Solve);
            Assert.That(task.Wait(SolveBound), Is.True,
                $"{label} did not terminate within {SolveBound.TotalSeconds:F0} s — runaway regression");
            return task.Result;
        }

        // Guards against "non-empty but wrong" plans that a bare IsEmpty assertion waves through.
        protected static void AssertPlanIsValid(GpProblem problem, GpSolutionSet solution) {
            Assert.That(solution.IsEmpty, Is.False, "expected a plan to validate");

            var plan = solution.GetSolution(0);
            var state = new HashSet<ISentence>(problem.InitialState);

            for (var layer = 0; layer < plan.Count; layer++) {
                var actions = plan[layer].GetActions(ignorePersistence: true);
                foreach (var action in actions) {
                    foreach (var precondition in action.Preconditions.Distinct()) {
                        Assert.That(state, Does.Contain(precondition),
                            $"layer {layer}: precondition {precondition} of {action.Name} " +
                            "does not hold in the simulated state");
                    }
                }

                foreach (var action in actions) {
                    foreach (var effect in action.Effects) {
                        state.Remove(effect.Negated());
                        state.Add(effect);
                    }
                }
            }

            foreach (var goal in problem.Goals) {
                Assert.That(state, Does.Contain(goal),
                    $"goal {goal} does not hold after executing the plan");
            }
        }
    }
}
