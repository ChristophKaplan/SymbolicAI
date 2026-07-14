using System.Collections.Generic;
using AIPlanning.Planning.GraphPlan;
using NUnit.Framework;

namespace AIPlanningTests {
    // Repro tests for the open findings of the July 2026 whole-project reviews. Each test
    // asserts the CORRECT behavior, so a failure here confirms the corresponding bug is
    // real and still present. Once a bug is fixed its test doubles as a regression pin.
    [TestFixture]
    [Category("OpenIssue")]
    public class OpenIssueTests : PlanningTestBase {

        // Finding 2 — IsEffectsApplicable adds the effect-vs-precondition binding to the
        // PRODUCER's unificator set, so a consumer variable that occurs only in preconditions
        // and is instantiable only via another action's effect is never bound; every consumer
        // instance is dropped as non-ground and the planner reports "no plan".
        [Test]
        public void Finding02_PreconditionOnlyVariable_BoundViaProducerEffect_IsSolvable() {
            var initialState = Factory.StringToSentence(new() { "Subject(Bob)" });
            var goals = Factory.StringToSentence(new() { "Fed(Bob)" });

            var bake = Factory.Create("Bake", new() { "Subject(z)" }, new() { "Have(Bread)" });
            var eat = Factory.Create("Eat", new() { "Have(x)", "Subject(z)" }, new() { "Fed(z)" });

            var problem = new GpProblem(initialState, goals, new() { bake, eat });
            var solution = SolveWithGuard(problem);

            Assert.That(solution.IsEmpty, Is.False,
                "Bake produces Have(Bread), then Eat with x=Bread yields Fed(Bob) — a valid " +
                "2-step plan; an empty solution means Eat's precondition-only variable x was " +
                "never bound from the producer's effect");
        }

        // Finding 5 — IsApplicableToPreconditions compares the Distinct()-ed matched-node
        // count against the raw Preconditions.Count, so a duplicated precondition literal
        // makes the action permanently inapplicable (mirror of the fixed duplicate-goals bug).
        [Test]
        public void Finding05_DuplicatePreconditionLiterals_ActionStillFires() {
            var initialState = Factory.StringToSentence(new() { "P(Obj)" });
            var goals = Factory.StringToSentence(new() { "G(Obj)" });

            var act = Factory.Create("Act", new() { "P(Obj)", "P(Obj)" }, new() { "G(Obj)" });

            var problem = new GpProblem(initialState, goals, new() { act });
            var solution = SolveWithGuard(problem);

            Assert.That(solution.IsEmpty, Is.False,
                "P(Obj) holds, so Act is applicable regardless of the duplicated precondition");
        }

        // Finding 5 (grounding-collapse variant) — the duplicate arises without user-written
        // duplicates when two distinct precondition variables ground to the same literal.
        [Test]
        public void Finding05_PreconditionsCollapsingUnderGrounding_ActionStillFires() {
            var initialState = Factory.StringToSentence(new() { "P(Obj)" });
            var goals = Factory.StringToSentence(new() { "G(Obj)" });

            var act = Factory.Create("Act", new() { "P(x)", "P(y)" }, new() { "G(x)" });

            var problem = new GpProblem(initialState, goals, new() { act });
            var solution = SolveWithGuard(problem);

            Assert.That(solution.IsEmpty, Is.False,
                "with Obj as the only object, x=y=Obj grounds Act to the duplicated " +
                "precondition {P(Obj), P(Obj)}, which must still count as satisfied");
        }

        // Finding 6 — InstantiateActions keys a dictionary by GpAction, whose equality is
        // content-based, so two content-equal actions in the problem's action list crash
        // Solve with ArgumentException instead of being deduplicated.
        [Test]
        public void Finding06_ContentEqualDuplicateActions_AreTolerated() {
            var initialState = Factory.StringToSentence(new() { "P(K)" });
            var goals = Factory.StringToSentence(new() { "G(K)" });
            var act = Factory.Create("Act", new() { "P(K)" }, new() { "G(K)" });
            var duplicate = Factory.Create("Act", new() { "P(K)" }, new() { "G(K)" });

            var problem = new GpProblem(initialState, goals, new() { act, duplicate });
            var solution = SolveWithGuard(problem);

            Assert.That(solution.IsEmpty, Is.False,
                "a duplicated action definition changes nothing semantically and must not " +
                "crash grounding or lose the one-step plan");
        }

        // Finding 7 — FindSolutions treats an empty belief state at levelIndex > 0 as failure
        // (its success arm is gated on the unreachable levelIndex < 0), so a trivially
        // satisfied empty goal set extracts as "no plan exists".
        [Test]
        public void Finding07_EmptyGoals_AtHigherLevel_AreTriviallySatisfied() {
            var initialState = Factory.StringToSentence(new() { "P(K)" });
            var goals = Factory.StringToSentence(new());
            var act = Factory.Create("X", new() { "P(K)" }, new() { "Q(K)" });
            var problem = new GpProblem(initialState, goals, new() { act });

            var graph = new GpPlanGraph(problem);
            graph.ExpandGraph();
            var solution = graph.ExtractSolution(1, new NoGoods());

            Assert.That(solution.IsEmpty, Is.False,
                "an empty goal set is satisfied by the empty plan at every level; IsEmpty " +
                "must mean 'no plan exists', not 'nothing was requested'");
        }

        // Finding 8 — GpBeliefState.Equals (and GpActionSet.Equals alike) uses equal counts
        // plus one-directional Contains, which is multiset-unsafe: {P, P} equals {P, Q}. The
        // IEnumerable constructor accepts duplicates, and NoGoods keys hash sets on this
        // equality — the same defect that was already fixed on GpAction.
        [Test]
        public void Finding08_BeliefStateEquality_IsMultisetSafe() {
            var p = new GpLiteralNode(L("P(Obj)"));
            var q = new GpLiteralNode(L("Q(Obj)"));

            var pp = new GpBeliefState(new List<GpNode> { p, p });
            var pq = new GpBeliefState(new List<GpNode> { p, q });

            Assert.That(pp.Equals(pq), Is.False,
                "{P, P} and {P, Q} are different states even though the counts match and " +
                "every element of the first occurs in the second");
        }

        // Finding 9 — MapPreConditionsToAction skips creating a literal node for a non-ground
        // precondition whenever ANY existing node unifies with it, even a strictly more
        // specific one; the general precondition then loses every grounding not covered by
        // that node, and solvability depends on action declaration order.
        [Test]
        public void Finding09_GeneralPrecondition_AfterSpecificOne_KeepsItsOwnGroundings() {
            var initialState = Factory.StringToSentence(new() { "P(K)" });
            var goals = Factory.StringToSentence(new() { "G(K)" });

            var specific = Factory.Create("ASpec", new() { "Q(Home)" }, new() { "G(K)" });
            var general = Factory.Create("AGen", new() { "Q(y)" }, new() { "G(K)" });
            var maker = Factory.Create("Maker", new() { "P(K)" }, new() { "Q(TreeA)" });

            var problem = new GpProblem(initialState, goals, new() { specific, general, maker });
            var solution = SolveWithGuard(problem);

            Assert.That(solution.IsEmpty, Is.False,
                "Maker then AGen(y=TreeA) is a valid 2-step plan; an empty solution means " +
                "AGen's precondition Q(y) was anchored onto the more specific existing node " +
                "Q(Home) and the binding y=TreeA was never collected");
        }

        [Test]
        public void Finding09_ControlCase_GeneralDeclaredFirst_IsSolvable() {
            var initialState = Factory.StringToSentence(new() { "P(K)" });
            var goals = Factory.StringToSentence(new() { "G(K)" });

            var general = Factory.Create("AGen", new() { "Q(y)" }, new() { "G(K)" });
            var specific = Factory.Create("ASpec", new() { "Q(Home)" }, new() { "G(K)" });
            var maker = Factory.Create("Maker", new() { "P(K)" }, new() { "Q(TreeA)" });

            var problem = new GpProblem(initialState, goals, new() { general, specific, maker });
            var solution = SolveWithGuard(problem);

            Assert.That(solution.IsEmpty, Is.False,
                "control: the same problem with AGen declared before ASpec — declaration " +
                "order must not change solvability");
        }
    }
}
