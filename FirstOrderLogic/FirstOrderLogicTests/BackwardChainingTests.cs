using System.Linq;
using FirstOrderLogic;
using NUnit.Framework;

namespace FolTests {
    public class BackwardChainingTests : TestBase {
        private bool Entails(params string[] kbThenGoal) {
            var goal = S(kbThenGoal.Last());
            var kb = Set(kbThenGoal.Take(kbThenGoal.Length - 1).ToArray());
            return new BackwardChaining().Entails(kb, goal);
        }

        [Test]
        public void PropositionalModusPonens() {
            Assert.That(Entails("A", "A => B", "B"), Is.True);
        }

        [Test]
        public void FirstOrder_SocratesIsMortal() {
            Assert.That(Entails("Human(Sokrates)", "Human(x) => Mortal(x)", "Mortal(Sokrates)"), Is.True);
        }

        [Test]
        public void FirstOrder_UnrelatedConstant_NotEntailed() {
            Assert.That(Entails("Human(Sokrates)", "Human(x) => Mortal(x)", "Mortal(Platon)"), Is.False);
        }

        [Test]
        public void Chains_ThroughMultipleRules() {
            Assert.That(Entails("P(a)", "P(x) => Q(x)", "Q(x) => R(x)", "R(a)"), Is.True);
        }

        [Test]
        public void ConjunctiveBody_BothPremisesMustHold() {
            Assert.That(
                Entails("Parent(Tom,Bob)", "Male(Tom)",
                        "(Parent(x,y) AND Male(x)) => Father(x,y)", "Father(Tom,Bob)"),
                Is.True);
        }

        // Recursive rule with a shared intermediate variable: Ancestor via Parent + (Parent ∧ Ancestor).
        // Exercises standardize-apart across recursion depth and a join on the middle term.
        [Test]
        public void RecursiveAncestor_IsProven() {
            Assert.That(
                Entails(
                    "Parent(Tom,Bob)", "Parent(Bob,Ann)",
                    "Parent(x,y) => Ancestor(x,y)",
                    "(Parent(x,z) AND Ancestor(z,y)) => Ancestor(x,y)",
                    "Ancestor(Tom,Ann)"),
                Is.True);
        }

        [Test]
        public void RecursiveAncestor_FalseGoal_NotEntailed() {
            Assert.That(
                Entails(
                    "Parent(Tom,Bob)", "Parent(Bob,Ann)",
                    "Parent(x,y) => Ancestor(x,y)",
                    "(Parent(x,z) AND Ancestor(z,y)) => Ancestor(x,y)",
                    "Ancestor(Ann,Tom)"),
                Is.False);
        }

        // A self-referential rule with no supporting fact must terminate (depth bound) and answer false,
        // never loop forever — the Prolog left-recursion trap.
        [Test]
        public void CyclicRule_Terminates_NotEntailed() {
            Assert.That(Entails("P(x) => P(x)", "P(a)"), Is.False);
        }

        [Test]
        public void NegatedQuery_NotDerivable_NotEntailed() {
            // ¬B is a legal goal now, but nothing derives it here.
            Assert.That(Entails("A", "A => B", "NOT B"), Is.False);
        }

        // ── Literal clauses: negation as explicit falsehood ──────────────────────

        [Test]
        public void NegativeGoal_ProvenViaNegativeHead() {
            Assert.That(Entails("IsFemale(mySelf)", "IsFemale(z) => NOT Work(z)", "NOT Work(mySelf)"), Is.True);
        }

        [Test]
        public void NegativePremise_ProvenFromNegativeFact() {
            Assert.That(
                Entails("NOT Employed(a)", "Subject(a)",
                        "(Subject(x) AND NOT Employed(x)) => NeedsJob(x)", "NeedsJob(a)"),
                Is.True);
        }

        // Explicit negation, not negation-as-failure: an absent positive fact proves nothing.
        [Test]
        public void NegativePremise_AbsenceIsNotNegation() {
            Assert.That(
                Entails("Subject(a)",
                        "(Subject(x) AND NOT Employed(x)) => NeedsJob(x)", "NeedsJob(a)"),
                Is.False);
        }

        // Polarity is part of the goal-head match: Work(x) never resolves a ¬Work goal or vice versa.
        [Test]
        public void PolarityMismatch_NotProven() {
            Assert.That(Entails("NOT Work(a)", "Work(x) => Tired(x)", "Tired(a)"), Is.False);
        }
    }
}
