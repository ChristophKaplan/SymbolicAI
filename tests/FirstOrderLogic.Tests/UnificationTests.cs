using FirstOrderLogic;
using NUnit.Framework;

namespace FolTests {
    public class UnificationTests : TestBase {
        [Test]
        public void Unify_VariablesAndConstants() {
            var u = new Unificator(S("P(x,y,y)"), S("P(y,z,a)"));
            Assert.That(u.IsUnifiable, Is.True);
        }

        [Test]
        public void Unify_OccursCheckFails() {
            var u = new Unificator(S("P(x,y,y)"), S("P(f(y),y,x)"));
            Assert.That(u.IsUnifiable, Is.False);
        }

        [Test]
        public void Unify_ConflictingBindingsFail() {
            var u = new Unificator(S("P(f(x),a,x)"), S("P(f(g(y)),z,z)"));
            Assert.That(u.IsUnifiable, Is.False);
        }

        [Test]
        public void Unify_DifferentPredicateSymbolsFail() {
            var u = new Unificator(S("R(x)"), S("S(x)"));
            Assert.That(u.IsUnifiable, Is.False);
        }

        [Test]
        public void Unify_SameArityDifferentConstantsFail() {
            var u = new Unificator(S("P(a)"), S("P(b)"));
            Assert.That(u.IsUnifiable, Is.False);
        }

        [Test]
        public void Unify_IdenticalGroundLiteralsSucceedWithNoSubstitution() {
            var u = new Unificator(S("P(a)"), S("P(a)"));
            Assert.That(u.IsUnifiable, Is.True);
            Assert.That(u.IsEmpty, Is.True);
        }

        [Test]
        public void Unify_PropositionsBySymbol() {
            Assert.That(new Unificator(S("A"), S("A")).IsUnifiable, Is.True);
            Assert.That(new Unificator(S("A"), S("B")).IsUnifiable, Is.False);
        }

        [Test]
        public void Unify_PredicateAndPropositionDoNotUnify() {
            Assert.That(new Unificator(S("P(a)"), S("P")).IsUnifiable, Is.False);
        }
    }
}
