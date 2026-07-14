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

        // Parsed TRUE/FALSE are Propositions with fixed truth values — they must evaluate
        // without an assignment entry.
        [Test]
        public void World_EvaluatesParsedBooleanConstants() {
            var empty = new PossibleWorld(new Dictionary<IProposition, bool>());
            Assert.That(empty.Evaluate(S("TRUE")), Is.True);
            Assert.That(empty.Evaluate(S("FALSE")), Is.False);

            // B is false and A is true here, so the outcome hinges on the constants themselves.
            Assert.That(World(true, false).Evaluate(S("B OR TRUE")), Is.True);
            Assert.That(World(true, false).Evaluate(S("A AND FALSE")), Is.False);
            Assert.That(World(true, false).Evaluate(S("NOT FALSE")), Is.True);
        }

        [Test]
        public void World_UnknownProposition_ThrowsInterpretationException() {
            Assert.That(
                () => World(true, false).Evaluate(S("C")),
                Throws.TypeOf<InterpretationException>());
        }

        // A genuine (non-constant) function symbol: succ over a small numeric domain,
        // evaluated inside a predicate.
        [Test]
        public void Evaluate_FunctionSymbol_AppliedInsidePredicate() {
            IDomainOfDiscourse domain = new Domain(new Element(1), new Element(2), new Element(3));
            var relations = new Dictionary<string, Func<IElementOfDiscourse[], bool>>
            {
                ["IsTwo"] = terms => terms[0] is Element e && e.Id == 2,
            };
            Interpretation interpretation = null!;
            var functions = new Dictionary<string, Func<Term[], IElementOfDiscourse>>
            {
                ["one"] = _ => new Element(1),
            };
            functions["succ"] = terms => new Element(((Element)interpretation.EvaluateTerm(terms[0])).Id + 1);
            interpretation = new Interpretation(domain, relations, functions,
                new Dictionary<string, IElementOfDiscourse>(),
                new Dictionary<IProposition, bool>());

            Assert.That(interpretation.Evaluate(S("IsTwo(succ(one))")), Is.True);
            Assert.That(interpretation.Evaluate(S("IsTwo(succ(succ(one)))")), Is.False);
            Assert.That(interpretation.Evaluate(S("IsTwo(one)")), Is.False);
        }

        // Detach skips rules whose symbols the model does not cover (InterpretationException)
        // but must not swallow anything else.
        [Test]
        public void Detach_SkipsRulesOutsideTheModel_KeepsEvaluableOnes() {
            var interpretation = BuildInterpretationWithSokrates();
            var rules = new List<ISentence>
            {
                S("Human(Sokrates) => Mortal(Sokrates)"),
                S("Unknown(Sokrates) => Immortal(Sokrates)"),
            };
            var detached = SemanticChaining.Detach(rules, interpretation);
            Assert.That(detached.Count, Is.EqualTo(1));
            Assert.That(detached[0], Is.EqualTo(S("Mortal(Sokrates)")));
        }

        private static Interpretation BuildInterpretationWithSokrates() {
            IDomainOfDiscourse domain = new Domain(new Element(1), new Element(2));
            var relations = new Dictionary<string, Func<IElementOfDiscourse[], bool>>
            {
                ["Human"] = terms => terms[0] is Element e && e.Id == 1,
            };
            var functions = new Dictionary<string, Func<Term[], IElementOfDiscourse>>
            {
                ["Sokrates"] = _ => new Element(1),
            };
            return new Interpretation(domain, relations, functions,
                new Dictionary<string, IElementOfDiscourse>(),
                new Dictionary<IProposition, bool>());
        }
    }
}
