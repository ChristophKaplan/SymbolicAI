using System;
using FirstOrderLogic;
using NUnit.Framework;

namespace FolTests {
    // These cases all failed (returned false or never terminated) before GetResolvents
    // standardized shared variable names apart.
    public class StandardizeApartTests : TestBase {
        // KB ∀x P(x,A) entails ∃x P(B,x) via P(B,A); the negated goal clause {¬P(B,x)} reuses
        // the name x, which without renaming binds x→B and then fails on A vs x.
        [TestCase("P(x, A)", "P(B, x)")]
        // Same conflict between two KB clauses: {P(x,A)} and {¬P(B,x), Q(C)}.
        [TestCase("(P(x, A)) AND ((P(B, x)) => Q(C))", "Q(C)")]
        // Occurs-check trap: P(x) vs ¬P(f(x)) only unifies after renaming (x' → f(x)).
        [TestCase("P(x)", "P(f(x))")]
        // Control: distinct names — this already worked before the fix.
        [TestCase("P(x, A)", "P(B, y)")]
        public void Entailed_SharedVariableNames(string kb, string goal) {
            AssertResolves(kb, goal, expected: true);
        }

        [TestCase("P(A, x)", "P(B, x)")]
        [TestCase("P(x, A)", "P(x, B)")]
        public void NotEntailed_SharedVariableNames(string kb, string goal) {
            AssertResolves(kb, goal, expected: false);
        }

        // {¬P(x), Q(y)} and {¬Q(x), P(y)} keep producing alpha-variants of each other's
        // resolvents. Without canonical variable renaming the seen-set never saturates and
        // this query loops forever instead of answering false.
        [Test]
        public void NotEntailed_AlphaVariantResolvents_StillSaturates() {
            RunWithin(TimeSpan.FromSeconds(10), "Resolution (standardize-apart regression guard)", () =>
                AssertResolves("((NOT P(x)) OR Q(y)) AND ((NOT Q(x)) OR P(y))", "R", expected: false));
        }

        // ∀x P(x)→P(f(x)) generates P(f(a)), P(f(f(a))), … forever; the budget must cut it off.
        [Test]
        public void MaxRounds_Throws_WhenSaturationBudgetExceeded() {
            Assert.Throws<InvalidOperationException>(() =>
                Resolution.Resolve(S("P(a) AND ((P(x)) => P(f(x)))"), S("Q(b)"), maxRounds: 2));
        }

        [Test]
        public void MaxRounds_DoesNotAffectQueriesWithinBudget() {
            Assert.That(Resolution.Resolve(S("A AND (A => B)"), S("B"), maxRounds: 10), Is.True);
            Assert.That(Resolution.Resolve(S("A"), S("B"), maxRounds: 10), Is.False);
        }
    }
}
