using System;
using System.Linq;
using FirstOrderLogic;
using NUnit.Framework;

namespace FolTests {
    // Regression tests pinning the correctness bugs found (and fixed) in the July 2026
    // code review. Each test failed against the defect it describes.
    [Category("Regression")]
    public class ReviewRegressionTests : TestBase {
        private static readonly TimeSpan Bound = TimeSpan.FromSeconds(5);

        // Fixed defect: Unificator.cs — UnifyVar occurs-checks the raw term without dereferencing
        // existing bindings, so P(x,f(x)) vs P(f(y),y) "unifies" with cyclic {x→f(y), y→f(x)}.
        [Test]
        public void Issue01_OccursCheck_MustDereferenceBindings() {
            var u = new Unificator(S("P(x,f(x))"), S("P(f(y),y)"));
            Assert.That(u.IsUnifiable, Is.False,
                $"x = f(f(x)) has no solution; got substitutions: {u}");
        }

        // Fixed defect: Resolution.cs — binary resolution without a separate factoring rule cannot
        // refute {P(x) ∨ P(y)} ∪ {¬P(z) ∨ ¬P(w)}, which is unsatisfiable.
        [Test]
        public void Issue02_Resolution_NeedsFactoring_ReportsUnsatisfiable() {
            var clauses = S("(P(x) OR P(y)) AND ((NOT P(z)) OR (NOT P(w)))");
            var refuted = RunWithin(Bound, "Resolution", () => Resolution.IsUnsatisfiable(clauses, false, 200));
            Assert.That(refuted, Is.True, "clause set is unsatisfiable but was not refuted");
        }

        // Fixed defect: Sentence.cs — the recursion drops the boundVariables argument, so
        // HasScopeConflict never sees an enclosing binder and always returns false.
        [Test]
        public void Issue03_HasScopeConflict_DetectsNestedRebinding() {
            var s = S("FORALL x (P(x) AND (EXISTS x Q(x)))");
            Assert.That(s.HasScopeConflict(), Is.True);
        }

        // Fixed defect: BackwardChaining.cs — Prove expands only fresh.Premises and ignores
        // NafPremises, so a rule blocked by NAF still fires in backward chaining.
        [Test]
        public void Issue04_BackwardChaining_MustRespectNafPremises() {
            var kb = Set("Car(myCar)", "Broken(myCar)", "(Car(x) AND NAF Broken(x)) => Works(x)");
            var goal = S("Works(myCar)");
            Assert.That(ForwardChaining.Entails(kb, goal), Is.False,
                "sanity: forward chaining must not entail Works(myCar)");
            Assert.That(new BackwardChaining().Entails(kb, goal), Is.False,
                "NAF Broken(myCar) fails (Broken(myCar) is derivable), so the rule must not fire");
        }

        // Fixed defect: FirstOrderLogic.cs — only {x,y,z,w} parse as variables, so in
        // "FORALL p (Person(p))" the quantifier binds nothing: the term p becomes a Constant.
        [Test]
        public void Issue05_Quantifier_BindsNonWhitelistedVariableName() {
            var s = (IComplexSentence)Logic.TryParse("FORALL p (Person(p))");
            var body = (Predicate)s.Children[0];
            Assert.That(body.Terms[0], Is.InstanceOf<Variable>(),
                $"term 'p' under FORALL p parsed as {body.Terms[0].GetType().Name}");
        }

        // Fixed defect: grammar has no operator precedence — "A AND B OR C" should conventionally be
        // (A AND B) OR C (root OR), but parses right-nested as A AND (B OR C) (root AND).
        [Test]
        public void Issue06_Precedence_ConjunctionBindsTighterThanDisjunction() {
            var s = (IComplexSentence)S("A AND B OR C");
            Assert.That(s.Connective.Symbol, Is.EqualTo(Connective.LogicSymbol.DISJUNCTION),
                $"parsed as: {s}");
        }

        // Fixed defect: unary/binary conflation in the grammar — a leading binary connective is
        // accepted as a unary node instead of being rejected.
        [Test]
        public void Issue07_MalformedInput_LeadingBinaryConnective_Throws() {
            Assert.That(() => Logic.TryParse("AND P"), Throws.Exception);
        }

        // Fixed defect: unary/binary conflation in the grammar — NOT is accepted in a binary position,
        // producing a corrupt two-child negation node instead of a parse error.
        [Test]
        public void Issue07_MalformedInput_BinaryNot_Throws() {
            Assert.That(() => Logic.TryParse("P NOT Q"), Throws.Exception);
        }

        // Fixed defect: FirstOrderLogicExtensions.cs — skolemCounter is local to each SkolemForm
        // call, so independently skolemized sentences reuse the same Skolem name (sk1).
        [Test]
        public void Issue08_SkolemNames_UniqueAcrossCalls() {
            var s1 = (Predicate)S("EXISTS x (P(x))").SkolemForm();
            var s2 = (Predicate)S("EXISTS y (Q(y))").SkolemForm();
            Assert.That(s1.Terms[0].TermSymbol, Is.Not.EqualTo(s2.Terms[0].TermSymbol),
                $"both skolemizations produced '{s1.Terms[0].TermSymbol}'");
        }

        private sealed class ToggleSemantics : Semantics {
            public bool HumanHolds = true;
            protected override Signature Signature => new Signature.Builder()
                .Predicate("Human", 1).Constant("a").Build();
            protected override void Define() {
                var holds = HumanHolds;
                Relations["Human"] = _ => holds;
                Functions["a"] = _ => new Element(1);
            }
        }

        // Fixed defect: Semantic\Semantics.cs — BuildInterpretation hands its own mutable
        // dictionaries to every Interpretation and Clear()s them on the next build, so an earlier
        // interpretation silently changes.
        [Test]
        public void Issue09_BuildInterpretation_EarlierInterpretationUnaffectedByLaterBuild() {
            var semantics = new ToggleSemantics();
            var domain = new Domain(new Element(1));

            var first = semantics.BuildInterpretation(domain);
            Assert.That(first.Evaluate(S("Human(a)")), Is.True, "sanity: first build");

            semantics.HumanHolds = false;
            var second = semantics.BuildInterpretation(domain);
            Assert.That(second.Evaluate(S("Human(a)")), Is.False, "sanity: second build");

            Assert.That(first.Evaluate(S("Human(a)")), Is.True,
                "first interpretation changed after the second build (aliased dictionaries)");
        }

        // Fixed defect: Rule.From accepts function-containing "safe" rules and ForwardChaining.Saturate
        // is unbounded, so {P(a), P(x) ⇒ P(f(x))} derives P(f(a)), P(f(f(a))), … forever.
        [Test]
        public void Issue10_ForwardChaining_FunctionSymbols_MustTerminate() {
            var kb = Set("P(a)", "P(x) => P(f(x))");
            RunWithin(Bound, "Saturate", () => {
                try {
                    ForwardChaining.Saturate(kb);
                }
                catch (ArgumentException) {
                    // Rejecting the rule up front is a sound way to terminate.
                }
            });
        }

        // Fixed defect: Unificator.cs — Apply performs one sequential pass over the triangular
        // substitution {x→a, z→f(x)}, so Q(z) becomes Q(f(x)) instead of Q(f(a)).
        [Test]
        public void Issue11_UnificatorApply_ResolvesChainedBindings() {
            var u = new Unificator(S("P(x,z)"), S("P(a,f(x))"));
            Assert.That(u.IsUnifiable, Is.True, "sanity: P(x,z) and P(a,f(x)) unify");

            var applied = (Predicate)u.Apply(S("Q(z)"));
            var freeVariables = applied.GetVariables().Select(v => v.TermSymbol).ToList();
            Assert.That(freeVariables, Has.No.Member("x"),
                $"applying {{{u}}} to Q(z) yielded {applied}; expected Q(f(a))");
        }
    }
}
