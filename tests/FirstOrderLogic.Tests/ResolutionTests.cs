using FirstOrderLogic;
using NUnit.Framework;

namespace FolTests {
    public class ResolutionTests : TestBase {
        private static bool Resolve(ISentence kb, ISentence goal) => Resolution.Resolve(kb, goal);

        [Test]
        public void Resolve_SocratesIsMortal() {
            var pnf = S("(Human(Sokrates) AND (FORALL x (Human(x) => Mortal(x))))").ToPrenexForm(out _);
            var skolem = pnf.SkolemForm();
            Assert.That(Resolve(skolem, S("Mortal(Sokrates)")), Is.True);
        }

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

        [Test]
        public void Resolve_ExistentialKb() {
            Assert.That(Resolve(
                S("(EXISTS x Human(x)) AND (FORALL y (Human(y) => Mortal(y)))"),
                S("EXISTS z Mortal(z)")), Is.True);
        }

        // Capture regression: pulling EXISTS x over the free (universal) x of Human(x)
        // without renaming would shrink "everything is human" to "sk1 is human".
        [Test]
        public void Resolve_FreeVariableNotCapturedByPulledQuantifier() {
            Assert.That(Resolve(S("Human(x) AND (EXISTS x Robot(x))"), S("Human(Sokrates)")), Is.True);
        }

        [Test]
        public void Resolve_SameNameExistentialAndUniversal() {
            Assert.That(Resolve(
                S("(EXISTS x Human(x)) AND (FORALL x (Human(x) => Mortal(x)))"),
                S("EXISTS y Mortal(y)")), Is.True);
        }

        [Test]
        public void Resolve_UnrelatedPredicate_NotEntailed() {
            Assert.That(Resolve(S("P(a)"), S("Z(a)")), Is.False);
        }

        [Test]
        public void Resolve_ComplexConsequence() {
            Assert.That(Resolve(S("A AND B"), S("A AND B")), Is.True);
            Assert.That(Resolve(S("A"), S("A AND B")), Is.False);
        }

        [Test]
        public void Resolve_FirstOrderModusPonens() {
            var kb = S("Human(Sokrates) AND (Human(x) => Mortal(x))").ToConjunctiveNormalForm(out _);
            Assert.That(Resolve(kb, S("Mortal(Sokrates)")), Is.True);
        }

        [Test]
        public void Resolve_DisjunctiveSyllogism() {
            var kb = S("(A OR B) AND (NOT A)").ToConjunctiveNormalForm(out _);
            Assert.That(Resolve(kb, S("B")), Is.True);
        }
    }
}
