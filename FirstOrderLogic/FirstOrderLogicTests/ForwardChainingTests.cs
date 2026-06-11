using System.Linq;
using FirstOrderLogic;
using NUnit.Framework;

namespace FolTests {
    public class ForwardChainingTests : TestBase {
        private bool Entails(params string[] kbThenGoal) {
            var goal = S(kbThenGoal.Last());
            var kb = Set(kbThenGoal.Take(kbThenGoal.Length - 1).ToArray());
            return new ForwardChaining().Entails(kb, goal);
        }

        [Test]
        public void PropositionalModusPonens() {
            Assert.That(Entails("A", "A => B", "B"), Is.True);
        }

        // The canonical first-order example: a fact plus a universally-quantified rule.
        [Test]
        public void FirstOrder_SocratesIsMortal() {
            Assert.That(Entails("Human(Sokrates)", "Human(x) => Mortal(x)", "Mortal(Sokrates)"), Is.True);
        }

        [Test]
        public void FirstOrder_UnrelatedConstant_NotEntailed() {
            Assert.That(Entails("Human(Sokrates)", "Human(x) => Mortal(x)", "Mortal(Platon)"), Is.False);
        }

        // Transitive chain through two rules — the fixpoint must keep firing past the first round.
        [Test]
        public void Chains_ThroughMultipleRules() {
            Assert.That(Entails("P(a)", "P(x) => Q(x)", "Q(x) => R(x)", "R(a)"), Is.True);
        }

        // A conjunctive body: both premises must be satisfied by the same binding.
        [Test]
        public void ConjunctiveBody_BothPremisesMustHold() {
            Assert.That(
                Entails("Parent(Tom,Bob)", "Male(Tom)",
                        "(Parent(x,y) AND Male(x)) => Father(x,y)", "Father(Tom,Bob)"),
                Is.True);
        }

        [Test]
        public void ConjunctiveBody_MissingPremise_NotEntailed() {
            // No Male(Tom) fact, so the rule never fires.
            Assert.That(
                Entails("Parent(Tom,Bob)",
                        "(Parent(x,y) AND Male(x)) => Father(x,y)", "Father(Tom,Bob)"),
                Is.False);
        }

        // A query with a free variable is entailed when some derived fact instantiates it.
        [Test]
        public void VariableQuery_EntailedByInstance() {
            Assert.That(Entails("Human(Sokrates)", "Human(x) => Mortal(x)", "Mortal(x)"), Is.True);
        }

        [Test]
        public void NegatedQuery_NeverEntailed() {
            Assert.That(Entails("A", "A => B", "NOT B"), Is.False);
        }

        [Test]
        public void Saturate_ReturnsDeductiveClosure() {
            var closure = new ForwardChaining().Saturate(
                Set("P(a)", "P(x) => Q(x)", "Q(x) => R(x)"));
            Assert.That(closure, Has.Member(S("P(a)")));
            Assert.That(closure, Has.Member(S("Q(a)")));
            Assert.That(closure, Has.Member(S("R(a)")));
            Assert.That(closure.Count, Is.EqualTo(3));
        }
    }
}
