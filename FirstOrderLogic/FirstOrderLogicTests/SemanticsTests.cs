using System;
using System.Collections.Generic;
using FirstOrderLogic;
using NUnit.Framework;

namespace FolTests {
    public class SemanticsTests : TestBase {
        // Domain {1,2,3,4}; Human and Mortal both hold of elements 1 and 2.
        private Interpretation BuildInterpretation() {
            IDomainOfDiscourse domain = new Domain(new Element(1), new Element(2), new Element(3), new Element(4));
            var relations = new Dictionary<string, Func<IElementOfDiscourse[], bool>>
            {
                ["Human"] = terms => terms[0] is Element e && (e.Id == 1 || e.Id == 2),
                ["Mortal"] = terms => terms[0] is Element e && (e.Id == 1 || e.Id == 2),
            };
            return new Interpretation(domain, relations,
                new Dictionary<string, Func<Term[], IElementOfDiscourse>>(),
                new Dictionary<string, IElementOfDiscourse>(),
                new Dictionary<IProposition, bool>());
        }

        [Test]
        public void Evaluate_UniversalImplication_True() {
            var s = (Sentence)Logic.TryParse("FORALL x (Human(x) => Mortal(x))");
            Assert.That(BuildInterpretation().Evaluate(s), Is.True);
        }

        [Test]
        public void Evaluate_UniversalAtom_FalseWhenNotAllSatisfy() {
            var s = (Sentence)Logic.TryParse("FORALL x Human(x)");
            Assert.That(BuildInterpretation().Evaluate(s), Is.False);
        }

        [Test]
        public void Evaluate_Existential_TrueWhenSomeSatisfy() {
            var s = (Sentence)Logic.TryParse("EXISTS x Human(x)");
            Assert.That(BuildInterpretation().Evaluate(s), Is.True);
        }

        [Test]
        public void Evaluate_Existential_FalseWhenNoneSatisfy() {
            var s = (Sentence)Logic.TryParse("EXISTS x (Human(x) AND (NOT Mortal(x)))");
            Assert.That(BuildInterpretation().Evaluate(s), Is.False);
        }

        // ── Propositional truth tables via PossibleWorld ──────────────────────────
        private static PossibleWorld World(bool a, bool b) => new(new Dictionary<IProposition, bool>
        {
            [new Proposition("A")] = a,
            [new Proposition("B")] = b,
        });

        [Test]
        public void World_EvaluatesConnectives() {
            Assert.That(World(true, false).Evaluate(S("A OR B")), Is.True);
            Assert.That(World(true, false).Evaluate(S("A AND B")), Is.False);
            Assert.That(World(true, false).Evaluate(S("NOT B")), Is.True);
            Assert.That(World(true, false).Evaluate(S("A => B")), Is.False);
            Assert.That(World(false, false).Evaluate(S("A <=> B")), Is.True);
        }
    }
}
