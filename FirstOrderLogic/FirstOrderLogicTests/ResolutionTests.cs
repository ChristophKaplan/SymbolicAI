using FirstOrderLogic;
using NUnit.Framework;

namespace FolTests {
    public class ResolutionTests : TestBase {
        private static bool Resolve(ISentence kb, ISentence goal) => Resolution.Resolve(kb, goal);

        // The README's worked example: Skolemized KB proves Mortal(Sokrates).
        [Test]
        public void Resolve_SocratesIsMortal() {
            var pnf = Logic.ToPrenexForm(S("(Human(Sokrates) AND (FORALL x (Human(x) => Mortal(x))))"), out _);
            var skolem = Logic.SkolemForm(pnf);
            Assert.That(Resolve(skolem, S("Mortal(Sokrates)")), Is.True);
        }

        // Same KB with explicit quantifiers, no manual pipeline: Resolve prenexes and
        // skolemizes internally.
        [Test]
        public void Resolve_QuantifiedKb_NoManualSkolemization() {
            Assert.That(Resolve(
                S("Human(Sokrates) AND (FORALL x (Human(x) => Mortal(x)))"),
                S("Mortal(Sokrates)")), Is.True);
        }

        [Test]
        public void Resolve_QuantifiedKb_NotEntailed() {
            Assert.That(Resolve(
                S("FORALL x (Human(x) => Mortal(x))"),
                S("Mortal(Sokrates)")), Is.False);
        }

        // An existential goal is negated into a universal before skolemization, so it must
        // resolve against the witness rather than being skolemized into one.
        [Test]
        public void Resolve_ExistentialConsequence() {
            Assert.That(Resolve(S("Human(Sokrates)"), S("EXISTS x Human(x)")), Is.True);
            Assert.That(Resolve(S("Mortal(Sokrates)"), S("EXISTS x Human(x)")), Is.False);
        }

        // An existential in the KB becomes a Skolem term that chains through the rule.
        [Test]
        public void Resolve_ExistentialKb() {
            Assert.That(Resolve(
                S("(EXISTS x Human(x)) AND (FORALL y (Human(y) => Mortal(y)))"),
                S("EXISTS z Mortal(z)")), Is.True);
        }

        // Variable capture regression: the KB's free x (implicitly universal — "everything is
        // human") shares its name with a bound existential. Pulling EXISTS x over Human(x)
        // without renaming would capture the free x, and skolemization would then shrink
        // "everything is human" to "sk1 is human", losing the entailment.
        [Test]
        public void Resolve_FreeVariableNotCapturedByPulledQuantifier() {
            Assert.That(Resolve(S("Human(x) AND (EXISTS x Robot(x))"), S("Human(Sokrates)")), Is.True);
        }

        // Same-named quantifiers of different kinds in separate conjuncts stay independent
        // through prenexing.
        [Test]
        public void Resolve_SameNameExistentialAndUniversal() {
            Assert.That(Resolve(
                S("(EXISTS x Human(x)) AND (FORALL x (Human(x) => Mortal(x)))"),
                S("EXISTS y Mortal(y)")), Is.True);
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
