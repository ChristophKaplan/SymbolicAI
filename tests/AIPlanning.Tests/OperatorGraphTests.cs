using System.Collections.Generic;
using System.Linq;
using AIPlanning.Planning.GraphPlan;
using FirstOrderLogic;

namespace AIPlanningTests {
    [TestFixture]
    public class OperatorGraphTests : PlanningTestBase {
        [Test]
        public void GetActionsForLiteral_GroundLiteral_FindsActionAnchoredAtVariableNode() {
            var initialState = Factory.StringToSentence(new() { "P(Obj)" });
            var goals = Factory.StringToSentence(new() { "R(Obj)" });
            var a1 = Factory.Create("A1", new() { "P(x)" }, new() { "Q(x)" });
            var a2 = Factory.Create("A2", new() { "Q(x)" }, new() { "R(x)" });

            var graph = new OperatorGraph(new GpProblem(initialState, goals, new() { a1, a2 }));

            var actions = graph.GetActionsForLiteral(L("Q(Obj)"));

            Assert.That(actions.Select(a => a.Signifier), Does.Contain("A2"),
                "the ground literal Q(Obj) must unify with the operator-graph node Q(x)");
            Assert.That(actions.Where(a => a.Signifier == "A2").All(a => a.IsGround()),
                Is.True, "surfaced instances must be fully ground");
        }

        [Test]
        public void GetActionsForLiteral_ReturnsNoNonGroundInstances() {
            var initialState = Factory.StringToSentence(new() { "P(Obj)" });
            var goals = Factory.StringToSentence(new() { "R(Obj)" });
            var a1 = Factory.Create("A1", new() { "P(x)" }, new() { "Q(x)" });
            var a2 = Factory.Create("A2", new() { "Q(x)" }, new() { "R(x)" });

            var graph = new OperatorGraph(new GpProblem(initialState, goals, new() { a1, a2 }));

            foreach (var literal in new[] { "P(Obj)", "Q(Obj)", "R(Obj)" }) {
                var actions = graph.GetActionsForLiteral(L(literal));
                var nonGround = actions.Where(a => !a.IsGround()).ToList();
                Assert.That(nonGround, Is.Empty,
                    $"instances with unbound variables can never match a ground belief state; " +
                    $"found for {literal}: [{string.Join("; ", nonGround)}]");
            }
        }

        [Test]
        public void GetActionsWithoutPreconditions_SurfacesPreconditionlessAction_ButNotStart() {
            var initialState = Factory.StringToSentence(new() { "Unrelated(Thing)" });
            var goals = Factory.StringToSentence(new() { "R(Obj)" });
            var spawn = Factory.Create("Spawn", new List<string>(), new() { "R(Obj)" });

            var graph = new OperatorGraph(new GpProblem(initialState, goals, new() { spawn }));

            var names = graph.GetActionsWithoutPreconditions().Select(a => a.Signifier).ToList();
            Assert.That(names, Does.Contain("Spawn"));
            Assert.That(names, Does.Not.Contain("Start"),
                "the synthetic Start action must never surface as a usable action");
            Assert.That(names, Does.Not.Contain("Finish"));
        }

        [Test]
        public void GetActionsForLiteral_NeverReturnsSyntheticStartOrFinish() {
            var initialState = Factory.StringToSentence(new() { "P(Obj)" });
            var goals = Factory.StringToSentence(new() { "G(Obj)" });
            var make = Factory.Create("Make", new() { "P(Obj)" }, new() { "G(Obj)" });

            var graph = new OperatorGraph(new GpProblem(initialState, goals, new() { make }));

            var forInitial = graph.GetActionsForLiteral(L("P(Obj)"));
            Assert.That(forInitial.Select(a => a.Signifier), Does.Contain("Make"));

            var forGoal = graph.GetActionsForLiteral(L("G(Obj)"));
            var allNames = forInitial.Concat(forGoal).Select(a => a.Signifier).ToList();
            Assert.That(allNames, Does.Not.Contain("Finish"),
                "Finish is attached to the goal literal during construction and must be filtered");
            Assert.That(allNames, Does.Not.Contain("Start"));
        }
    }
}
