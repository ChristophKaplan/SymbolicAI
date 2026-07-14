using FirstOrderLogic;
using NUnit.Framework;

namespace FolTests {
    public class SatTests : TestBase {
        private static readonly SatSolvers Sat = new();

        [Test]
        public void WalkSat_FindsModelForSatisfiableFormula() {
            var pnf = Logic.ToPrenexForm(S("(P => Q) AND R"), out _);
            var clauses = pnf.GetClauseSet();
            var model = Sat.WalkSAT(clauses, 0.5f, 200);
            Assert.That(model, Is.Not.Null);
            Assert.That(model!.Evaluate(clauses), Is.True);
        }

        [Test]
        public void WalkSat_SatisfiesLargerFormula() {
            var pnf = Logic.ToPrenexForm(S("(A OR B) AND ((NOT A) OR C) AND (B OR (NOT C))"), out _);
            var clauses = pnf.GetClauseSet();
            var model = Sat.WalkSAT(clauses, 0.5f, 500);
            Assert.That(model, Is.Not.Null);
            Assert.That(model!.Evaluate(clauses), Is.True);
        }

        // Unsatisfiable: no assignment can satisfy both A and ¬A.
        [Test]
        public void WalkSat_ReturnsNullForUnsatisfiable() {
            var clauses = S("A AND (NOT A)").GetClauseSet();
            Assert.That(Sat.WalkSAT(clauses, 0.5f, 100), Is.Null);
        }

        // p = 1 forces the random-walk branch on every iteration. Satisfying (¬A) AND (A OR B)
        // requires flipping B, so the walk must be able to reach ANY symbol of an unsatisfied
        // clause — the old code only ever flipped the first literal of a random clause (always A
        // here) and got stuck whenever B started out false.
        [Test]
        public void WalkSat_RandomWalk_CanFlipAnySymbolOfUnsatisfiedClause() {
            var clauses = S("(NOT A) AND (A OR B)").GetClauseSet();
            for (var i = 0; i < 10; i++) {
                var model = Sat.WalkSAT(clauses, 1f, 1000);
                Assert.That(model, Is.Not.Null);
                Assert.That(model!.Evaluate(clauses), Is.True);
            }
        }

        // WalkSAT is propositional only; first-order clauses must be rejected.
        [Test]
        public void WalkSat_RejectsNonPropositional() {
            var clauses = S("P(a) AND Q(b)").GetClauseSet();
            Assert.That(() => Sat.WalkSAT(clauses, 0.5f, 100), Throws.Exception);
        }
    }
}
