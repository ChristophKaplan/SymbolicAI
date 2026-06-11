using System;
using System.Collections.Generic;
using FirstOrderLogic;
using NUnit.Framework;

namespace FolTests
{
    public class SignatureTests : TestBase
    {
        private static Signature Sig() => new Signature.Builder()
            .Predicate("Human", 1)
            .Predicate("Loves", 2)
            .Constant("Sokrates")
            .Build();

        // ── Signature: well-formedness over a vocabulary ──────────────────────────

        [Test]
        public void Covers_TrueWhenAllPredicatesDeclared() =>
            Assert.That(Sig().Covers(S("Human(Sokrates)")), Is.True);

        [Test]
        public void Covers_FalseForUndeclaredPredicate() =>
            Assert.That(Sig().Covers(S("Mortal(Sokrates)")), Is.False);

        [Test]
        public void Covers_FalseForArityMismatch() =>
            Assert.That(Sig().Covers(S("Human(Sokrates, Plato)")), Is.False);

        [Test]
        public void Covers_RecursesIntoComplexSentences() =>
            Assert.That(Sig().Covers(S("Human(x) => Loves(x, Sokrates)")), Is.True);

        [Test]
        public void UndeclaredPredicates_ReportsSymbolAndArity()
        {
            Assert.That(Sig().UndeclaredPredicates(S("Mortal(Sokrates)")), Does.Contain("Mortal/1"));
            Assert.That(Sig().UndeclaredPredicates(S("Human(Sokrates)")), Is.Empty);
        }

        [Test]
        public void Constants_AreArityZeroFunctions()
        {
            var sig = Sig();
            Assert.That(sig.HasConstant("Sokrates"), Is.True);
            Assert.That(sig.Constants, Does.Contain("Sokrates"));
            Assert.That(sig.HasPredicate("Loves", 2), Is.True);
            Assert.That(sig.HasPredicate("Loves", 1), Is.False);
        }

        // ── Signature.Symbol: name + arity as a declared vocabulary entry ─────────

        [Test]
        public void Symbol_ConvertsImplicitlyToItsName()
        {
            var owns = new Signature.Symbol("Owns", 2);
            string name = owns;
            Assert.That(name, Is.EqualTo("Owns"));
            Assert.That(owns.ToString(), Is.EqualTo("Owns"));
        }

        [Test]
        public void Symbol_OfRendersConcreteSyntax()
        {
            Assert.That(new Signature.Symbol("Owns", 2).Of("z", "y"), Is.EqualTo("Owns(z, y)"));
            Assert.That(new Signature.Symbol("Subject", 1).Of("y"), Is.EqualTo("Subject(y)"));
        }

        [Test]
        public void Symbol_OfThrowsOnArityMismatch() =>
            Assert.That(() => new Signature.Symbol("Owns", 2).Of("z"), Throws.TypeOf<ArgumentException>());

        [Test]
        public void Builder_AcceptsSymbol()
        {
            var role = new Signature.Symbol("Role", 2);
            var sig = new Signature.Builder().Predicate(role).Build();
            Assert.That(sig.HasPredicate("Role", 2), Is.True);
        }

        [Test]
        public void Symbol_OfRendersBareNameForConstant() =>
            Assert.That(new Signature.Symbol("Money", 0).Of(), Is.EqualTo("Money"));

        [Test]
        public void Builder_AcceptsFunctionSymbol()
        {
            var sig = new Signature.Builder().Function(new Signature.Symbol("fatherOf", 1)).Build();
            Assert.That(sig.HasFunction("fatherOf"), Is.True);
            Assert.That(sig.HasConstant("fatherOf"), Is.False);
        }

        [Test]
        public void Builder_AcceptsConstantSymbol()
        {
            var sig = new Signature.Builder().Constant(new Signature.Symbol("Money", 0)).Build();
            Assert.That(sig.HasConstant("Money"), Is.True);
            Assert.That(sig.Constants, Does.Contain("Money"));
        }

        // ── Semantics base: enforces the signature ↔ interpretation contract ──────

        // Declares Human (interpreted) + Role (derived/exempt) + constant Sokrates; only Human and
        // the constant are interpreted — Role intentionally has no relation.
        private sealed class CoveringSemantics : Semantics
        {
            protected override Signature Signature => new Signature.Builder()
                .Predicate("Human", 1).Predicate("Role", 2).Constant("Sokrates").Build();
            protected override IReadOnlyCollection<string> DerivedPredicates => new[] { "Role" };
            protected override void Define()
            {
                Relations["Human"]   = _ => true;
                Functions["Sokrates"] = _ => new Element(1);
            }
        }

        private sealed class MissingRelationSemantics : Semantics
        {
            protected override Signature Signature => new Signature.Builder().Predicate("Human", 1).Build();
            protected override void Define() { /* no relation for the declared Human */ }
        }

        private sealed class StrayRelationSemantics : Semantics
        {
            protected override Signature Signature => new Signature.Builder().Predicate("Human", 1).Build();
            protected override void Define()
            {
                Relations["Human"] = _ => true;
                Relations["Ghost"] = _ => true; // undeclared symbol
            }
        }

        private sealed class MissingConstantSemantics : Semantics
        {
            protected override Signature Signature => new Signature.Builder()
                .Predicate("Human", 1).Constant("Sokrates").Build();
            protected override void Define() => Relations["Human"] = _ => true; // no function for Sokrates
        }

        [Test]
        public void BuildInterpretation_SucceedsWhenCovered_DerivedNeedsNoRelation()
        {
            var interpretation = new CoveringSemantics().BuildInterpretation(new Domain());
            Assert.That(interpretation, Is.Not.Null);
        }

        [Test]
        public void BuildInterpretation_ThrowsWhenDeclaredRelationMissing() =>
            Assert.That(() => new MissingRelationSemantics().BuildInterpretation(new Domain()),
                Throws.Exception);

        [Test]
        public void BuildInterpretation_ThrowsForUndeclaredRelation() =>
            Assert.That(() => new StrayRelationSemantics().BuildInterpretation(new Domain()),
                Throws.Exception);

        [Test]
        public void BuildInterpretation_ThrowsWhenDeclaredConstantMissing() =>
            Assert.That(() => new MissingConstantSemantics().BuildInterpretation(new Domain()),
                Throws.Exception);
    }
}
