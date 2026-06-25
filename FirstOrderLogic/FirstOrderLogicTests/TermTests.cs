using FirstOrderLogic;
using NUnit.Framework;

namespace FolTests {
    public class TermTests : TestBase {
        [Test]
        public void Variable_EqualsBySymbol() {
            Assert.That(new Variable("x"), Is.EqualTo(new Variable("x")));
            Assert.That(new Variable("x"), Is.Not.EqualTo(new Variable("y")));
        }

        [Test]
        public void Constant_EqualsBySymbol() {
            Assert.That(new Constant("a"), Is.EqualTo(new Constant("a")));
            Assert.That(new Constant("a"), Is.Not.EqualTo(new Constant("b")));
        }

        // A variable and a constant are different kinds of term even if they share a name,
        // and equality must be symmetric.
        [Test]
        public void VariableAndConstant_AreNeverEqual_Symmetrically() {
            var x = new Variable("x");
            var c = new Constant("x");
            Assert.That(x.Equals(c), Is.False);
            Assert.That(c.Equals(x), Is.False);
        }

        [Test]
        public void Function_EqualsBySignatureAndArguments() {
            var f1 = new Function("f", new Term[] { new Variable("x") });
            var f2 = new Function("f", new Term[] { new Variable("x") });
            var f3 = new Function("f", new Term[] { new Variable("y") });
            Assert.That(f1, Is.EqualTo(f2));
            Assert.That(f1, Is.Not.EqualTo(f3));
        }

        [Test]
        public void Function_EqualSignatureIgnoresArguments() {
            var f1 = new Function("f", new Term[] { new Variable("x") });
            var f2 = new Function("f", new Term[] { new Constant("a") });
            Assert.That(f1.EqualSignature(f2), Is.True);
        }

        [Test]
        public void GetVariables_CollectsNestedVariables() {
            var pred = (Predicate)Logic.TryParse("P(f(x),y,a)");
            var vars = pred.GetVariables();
            Assert.That(vars.Length, Is.EqualTo(2));
        }

        [Test]
        public void Occurs_DetectsVariableInsideFunction() {
            var x = new Variable("x");
            var fx = new Function("f", new Term[] { x });
            Assert.That(fx.Occurs(x), Is.True);
            Assert.That(fx.Occurs(new Variable("y")), Is.False);
        }

        [Test]
        public void Substitute_ReplacesMatchingVariable() {
            Assert.That(new Variable("x").Substitute(new Variable("x"), new Constant("a")),
                Is.EqualTo(new Constant("a")));
        }

        [Test]
        public void Substitute_RecursesIntoFunction_WithoutMutatingOriginal() {
            var fx = new Function("f", new Term[] { new Variable("x") });
            var result = fx.Substitute(new Variable("x"), new Constant("a"));
            Assert.That(result, Is.EqualTo(new Function("f", new Term[] { new Constant("a") })));
            Assert.That(fx, Is.EqualTo(new Function("f", new Term[] { new Variable("x") })));
        }

        [Test]
        public void Substitute_ReplacesWholeMatchingFunction() {
            var fx = new Function("f", new Term[] { new Variable("x") });
            var gfx = new Function("g", new Term[] { fx });
            Assert.That(gfx.Substitute(fx, new Variable("y")),
                Is.EqualTo(new Function("g", new Term[] { new Variable("y") })));
        }

        // Nothing matched ⇒ the same instance is returned (subterms are shared, not copied).
        [Test]
        public void Substitute_ReturnsSameInstance_WhenNothingMatches() {
            var fy = new Function("f", new Term[] { new Variable("y") });
            Assert.That(fy.Substitute(new Variable("x"), new Constant("a")), Is.SameAs(fy));
        }
    }
}
