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

        [Test]
        public void CyclicRule_Terminates_NotEntailed() {
            Assert.That(Entails("P(x) => P(x)", "P(a)"), Is.False);
        }

        [Test]
        public void NegatedQuery_NotDerivable_NotEntailed() {
            Assert.That(Entails("A", "A => B", "NOT B"), Is.False);
        }

        [Test]
        public void NegativeGoal_ProvenViaNegativeHead() {
            Assert.That(Entails("Penguin(pingu)", "Penguin(z) => NOT Flies(z)", "NOT Flies(pingu)"), Is.True);
        }

        [Test]
        public void NegativePremise_ProvenFromNegativeFact() {
            Assert.That(
                Entails("NOT Employed(a)", "Subject(a)",
                        "(Subject(x) AND NOT Employed(x)) => NeedsJob(x)", "NeedsJob(a)"),
                Is.True);
        }

        [Test]
        public void NegativePremise_AbsenceIsNotNegation() {
            Assert.That(
                Entails("Subject(a)",
                        "(Subject(x) AND NOT Employed(x)) => NeedsJob(x)", "NeedsJob(a)"),
                Is.False);
        }

        [Test]
        public void PolarityMismatch_NotProven() {
            Assert.That(Entails("NOT Work(a)", "Work(x) => Tired(x)", "Tired(a)"), Is.False);
        }
    }
}
