using System.Collections.Generic;
using AIPlanning.Planning.GraphPlan;
using NUnit.Framework;

namespace AIPlanningTests {
    // Repro tests for the open findings of the July 2026 whole-project review. Each test
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
    }
}
