using FirstOrderLogic;
using NUnit.Framework;

namespace FolTests {
    public class SentenceTests : TestBase {
        [Test]
        public void IsLiteral_AtomAndNegatedAtom() {
            Assert.That(S("P(x)").IsLiteral, Is.True);
            Assert.That(S("NOT P(x)").IsLiteral, Is.True);
            Assert.That(S("P(x) AND Q(x)").IsLiteral, Is.False);
            Assert.That(S("NOT (P(x) AND Q(x))").IsLiteral, Is.False);
        }

        [Test]
        public void IsPropositional_DistinguishesAtomsFromPredicates() {
            Assert.That(S("A OR (B AND C)").IsPropositional(), Is.True);
            Assert.That(S("(Human(Sokrates) AND (FORALL x (Human(x) => Mortal(x))))").IsPropositional(), Is.False);
        }

        [Test]
        public void IsGround_TrueWhenNoVariables() {
            Assert.That(S("P(a)").IsGround(), Is.True);
            Assert.That(S("P(f(a))").IsGround(), Is.True);
            Assert.That(S("P(x)").IsGround(), Is.False);
            Assert.That(S("P(f(x))").IsGround(), Is.False);
        }

        [Test]
        public void IsCNF_RecognizesConjunctionsOfDisjunctions() {
            Assert.That(S("(A OR B) AND (C OR D)").IsCNF(), Is.True);
            Assert.That(S("A AND B").IsCNF(), Is.True);
            Assert.That(S("A OR B").IsCNF(), Is.True);
            Assert.That(S("A => B").IsCNF(), Is.False);
            Assert.That(S("A <=> B").IsCNF(), Is.False);
            Assert.That(S("A OR (B AND C)").IsCNF(), Is.False);
        }

        [Test]
        public void IsDisjunctionOfLiterals() {
            Assert.That(S("A OR (NOT B) OR C").IsDisjunctionOfLiterals(), Is.True);
            Assert.That(S("A OR (B AND C)").IsDisjunctionOfLiterals(), Is.False);
        }

        [Test]
        public void GetLiterals_FlattensClause() {
            var literals = S("A OR (NOT B) OR C").GetLiterals();
            Assert.That(literals.Count, Is.EqualTo(3));
        }

        [Test]
        public void IsNegationOf_DetectsComplementaryPair() {
            var p = S("P(a)");
            var notP = S("NOT P(a)");
            Assert.That(notP.IsNegationOf(p), Is.True);
            Assert.That(p.IsNegationOf(notP), Is.True);
            Assert.That(p.IsNegationOf(S("P(b)")), Is.False);
        }

        [Test]
        public void Negate_OfNegationCancels() {
            var p = S("P(a)");
            var doubleNegated = p.Negate().Negate();
            Assert.That(doubleNegated, Is.EqualTo(p));
        }

        // Cloning must be deep: substituting on a clone never touches the original.
        [Test]
        public void Clone_IsDeepAndIndependent() {
            var original = S("P(x)");
            var clone = original.Clone();
            clone.SubstituteTerm(new Variable("x"), new Constant("a"));
            Assert.That(original, Is.EqualTo(S("P(x)")));
            Assert.That(clone, Is.EqualTo(S("P(a)")));
        }

        [Test]
        public void Equals_StructuralAndHashConsistent() {
            var a = S("(P(x) OR Q(y)) AND R(z)");
            var b = S("(P(x) OR Q(y)) AND R(z)");
            Assert.That(a, Is.EqualTo(b));
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        }

        [Test]
        public void SubstituteTerm_ReplacesInsideFunctions() {
            var s = S("P(f(x))");
            s.SubstituteTerm(new Variable("x"), new Constant("a"));
            Assert.That(s, Is.EqualTo(S("P(f(a))")));
        }

        // Pure variant: returns a new tree and leaves the original untouched.
        [Test]
        public void Substitute_IsPure_AndReplacesThroughout() {
            var original = S("P(x) AND Q(f(x))");
            var result = original.Substitute(new Variable("x"), new Constant("a"));
            Assert.That(result, Is.EqualTo(S("P(a) AND Q(f(a))")));
            Assert.That(original, Is.EqualTo(S("P(x) AND Q(f(x))")));
        }

        [Test]
        public void Substitute_PreservesQuantifier() {
            var result = S("FORALL x P(x,y)").Substitute(new Variable("y"), new Constant("a"));
            Assert.That(result, Is.EqualTo(S("FORALL x P(x,a)")));
        }

        // Time-indexed instances: rolling an action forward over [from, to).
        [Test]
        public void GetInstancesOverTime_ProducesShiftedCopies() {
            var action = S("Cook^0 => HaveIngredient^0 AND Food^1");
            var instances = action.GetInstancesOverTime(0, 3);
            Assert.That(instances.Count, Is.EqualTo(3));
            // Originals are untouched (clones were shifted).
            Assert.That(action, Is.EqualTo(S("Cook^0 => HaveIngredient^0 AND Food^1")));
        }
    }
}
