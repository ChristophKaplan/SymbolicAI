using FirstOrderLogic;
using NUnit.Framework;

namespace FolTests {
    public class ResolutionTests : TestBase {
        private static bool Resolve(ISentence kb, ISentence goal) => new Resolution().Resolve(kb, goal);

        // The README's worked example: Skolemized KB proves Mortal(Sokrates).
        [Test]
        public void Resolve_SocratesIsMortal() {
            var pnf = Logic.ToPrenexForm(S("(Human(Sokrates) AND (FORALL x (Human(x) => Mortal(x))))"), out _);
            var skolem = Logic.SkolemForm(pnf);
            Assert.That(Resolve(skolem, S("Mortal(Sokrates)")), Is.True);
        }

        [Test]
        public void Resolve_PropositionalModusPonens() {
            Assert.That(Resolve(S("((NOT A) OR B) AND A"), S("B")), Is.True);
        }

        [Test]
        public void Resolve_UnrelatedProposition_NotEntailed() {
            Assert.That(Resolve(S("P"), S("Z")), Is.False);
        }

        [Test]
        public void Resolve_UnrelatedPredicate_NotEntailed() {
            Assert.That(Resolve(S("P(a)"), S("Z(a)")), Is.False);
        }

        // Soundness: a tautology entails only tautologies, never a contingent atom.
        [Test]
        public void Resolve_Tautology_DoesNotEntailArbitrary() {
            Assert.That(Resolve(S("P OR (NOT P)"), S("Z")), Is.False);
            Assert.That(Resolve(S("P(a) OR (NOT P(a))"), S("Z(a)")), Is.False);
        }

        // The negated consequence may be complex (non-CNF); the resolver must normalize first.
        [Test]
        public void Resolve_ComplexConsequence() {
            Assert.That(Resolve(S("A AND B"), S("A AND B")), Is.True);
            Assert.That(Resolve(S("A"), S("A AND B")), Is.False);
        }

        // First-order chaining with a shared variable.
        [Test]
        public void Resolve_FirstOrderModusPonens() {
            var kb = Logic.ToConjunctiveNormalForm(S("Human(Sokrates) AND (Human(x) => Mortal(x))"), out _);
            Assert.That(Resolve(kb, S("Mortal(Sokrates)")), Is.True);
        }

        [Test]
        public void Resolve_DisjunctiveSyllogism() {
            var kb = Logic.ToConjunctiveNormalForm(S("(A OR B) AND (NOT A)"), out _);
            Assert.That(Resolve(kb, S("B")), Is.True);
        }
    }
}
