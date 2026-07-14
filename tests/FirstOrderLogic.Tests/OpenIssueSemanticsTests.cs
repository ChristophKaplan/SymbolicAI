using System;
using System.Collections.Generic;
using FirstOrderLogic;
using NUnit.Framework;

namespace FolTests {
    // Repro tests for the findings of the July 2026 semantics/model-theory review, all fixed
    // since. Each test asserts the CORRECT behavior and now serves as a regression pin.
    [Category("OpenIssue")]
    public class OpenIssueSemanticsTests : TestBase {
        // P holds of element 1 only (so P(a) tracks pHoldsOfA); Q holds of qWitness only.
        private static Interpretation Model(bool pHoldsOfA, int? qWitness) {
            IDomainOfDiscourse domain = new Domain(new Element(1), new Element(2));
            var relations = new Dictionary<string, Func<IElementOfDiscourse[], bool>> {
                ["P"] = args => pHoldsOfA && ((Element)args[0]).Id == 1,
                ["Q"] = args => ((Element)args[0]).Id == qWitness,
            };
            var functions = new Dictionary<string, Func<IElementOfDiscourse[], IElementOfDiscourse>> {
                ["a"] = _ => new Element(1),
                ["b"] = _ => new Element(2),
            };
            return new Interpretation(domain, relations, functions,
                new Dictionary<string, IElementOfDiscourse>(),
                new Dictionary<IProposition, bool>());
        }

        // Finding 40 — PullQuantifier's capture check only renamed when the pulled variable
        // occurred FREE in the sibling, so a VACUOUS binder whose name a sibling quantifier
        // reuses produced a shadowed prenex prefix (∀x ∃x …). The result is equivalent (the
        // shadowed binder is vacuous) but Interpretation.Evaluate rejects it as a scope conflict,
        // making it unusable for model evaluation.
        [Test]
        public void Finding40_PrenexForm_VacuousBinderSharingASiblingsName_IsEvaluable() {
            var prenex = S("(FORALL x (P(a))) AND (EXISTS x (Q(x)))").ToPrenexForm();

            Assert.That(prenex.HasScopeConflict(), Is.False,
                $"the prenex prefix must not shadow a binder; got: {prenex}");
            Assert.That(() => Model(pHoldsOfA: true, qWitness: 1).Evaluate(prenex), Throws.Nothing,
                $"a shadowed prefix is vacuous but unusable for model evaluation; got: {prenex}");
        }

        // The rename must not change what the sentence says — ToPrenexForm is equivalence-preserving.
        [Test]
        public void Finding40_PrenexForm_VacuousBinder_PreservesEquivalence() {
            var sentence = S("(FORALL x (P(a))) AND (EXISTS x (Q(x)))");
            var prenex = sentence.ToPrenexForm();

            foreach (var pHoldsOfA in new[] { true, false }) {
                foreach (var qWitness in new int?[] { null, 1, 2 }) {
                    var model = Model(pHoldsOfA, qWitness);
                    Assert.That(model.Evaluate(prenex), Is.EqualTo(model.Evaluate(sentence)),
                        $"prenex form disagrees with the original under P(a)={pHoldsOfA}, " +
                        $"Q witness={qWitness?.ToString() ?? "none"}: {sentence} vs {prenex}");
                }
            }
        }

        // The general shape is (Qv A) op (Q'v B) where one of the two binders is vacuous.
        [Test]
        public void Finding40_PrenexForm_ReusedBinderName_NeverShadows() {
            var bodies = new[] { ("P(a)", "Q(x)"), ("P(x)", "Q(b)"), ("P(a)", "Q(b)") };
            var prefixes = new[] {
                ("FORALL", "EXISTS"), ("EXISTS", "FORALL"),
                ("FORALL", "FORALL"), ("EXISTS", "EXISTS"),
            };

            foreach (var op in new[] { "AND", "OR" }) {
                foreach (var (left, right) in bodies) {
                    foreach (var (leftPrefix, rightPrefix) in prefixes) {
                        var sentence = S($"({leftPrefix} x ({left})) {op} ({rightPrefix} x ({right}))");
                        var prenex = sentence.ToPrenexForm();
                        Assert.That(prenex.HasScopeConflict(), Is.False,
                            $"{sentence} produced a shadowed prefix: {prenex}");
                    }
                }
            }
        }

        private sealed class SuccessorSemantics : Semantics {
            protected override Signature Signature => new Signature.Builder()
                .Predicate("IsTwo", 1).Function("succ", 1).Constant("one").Build();

            protected override void Define() {
                Relations["IsTwo"] = args => ((Element)args[0]).Id == 2;
                Functions["one"] = _ => new Element(1);
                Functions["succ"] = args => new Element(((Element)args[0]).Id + 1);
            }
        }

        // Finding 41 — function interpretation was non-compositional: relations received evaluated
        // elements but functions received raw syntactic Term[], so an interpreted function could
        // not resolve a bound-variable argument without calling back into the very Interpretation
        // being consulted — which a Semantics subclass has no access to from inside Define().
        // Defining any arity>0 function used under a quantifier was therefore impossible.
        [Test]
        public void Finding41_Semantics_ArityOneFunction_IsUsableUnderAQuantifier() {
            var model = new SuccessorSemantics().BuildInterpretation(new Domain(new Element(1)));
            Assert.That(model.Evaluate(S("FORALL x (IsTwo(succ(x)))")), Is.True,
                "the domain is {1} and succ(1)=2, so every element's successor is two; a throw " +
                "means Define() could not give succ its argument's binding");
        }

        [Test]
        public void Finding41_Semantics_ArityOneFunction_RespectsEachBinding() {
            var model = new SuccessorSemantics().BuildInterpretation(new Domain(new Element(1), new Element(2)));
            Assert.That(model.Evaluate(S("EXISTS x (IsTwo(succ(x)))")), Is.True, "succ(1)=2");
            Assert.That(model.Evaluate(S("FORALL x (IsTwo(succ(x)))")), Is.False, "succ(2)=3, not two");
            Assert.That(model.Evaluate(S("IsTwo(succ(one))")), Is.True, "nested ground term still works");
        }

        private sealed class WeatherSemantics : Semantics {
            protected override Signature Signature => new Signature.Builder()
                .Predicate("Raining", 0).Build();

            protected override void Define() => Relations["Raining"] = _ => true;
        }

        // Finding 42 — a signature-declared 0-ary predicate was unusable: Validate REQUIRED a
        // Relations entry for it, but a nullary atom parses as a Proposition, which evaluation
        // routed to PropositionalAssignments and never to Relations.
        [Test]
        public void Finding42_Semantics_DeclaredNullaryPredicate_EvaluatesEndToEnd() {
            var model = new WeatherSemantics().BuildInterpretation(new Domain(new Element(1)));
            Assert.That(model.Evaluate(S("Raining")), Is.True,
                "Validate accepts the relation for the declared 0-ary predicate, so evaluation " +
                "has to reach it too — applied to no arguments");
        }

        [Test]
        public void Finding42_NullaryRelation_DoesNotShadowTheBooleanConstants() {
            var model = new WeatherSemantics().BuildInterpretation(new Domain(new Element(1)));
            Assert.That(model.Evaluate(S("TRUE")), Is.True);
            Assert.That(model.Evaluate(S("FALSE")), Is.False);
        }

        private sealed class MissingFunctionSemantics : Semantics {
            protected override Signature Signature => new Signature.Builder()
                .Predicate("IsTwo", 1).Function("succ", 1).Build();

            protected override void Define() => Relations["IsTwo"] = _ => true;
        }

        // Finding 43 — Validate checked predicates and constants but NOT declared arity>0 function
        // symbols, despite the class doc claiming the structure is total over its signature, so a
        // declared succ/1 with no Functions entry validated and only failed later at evaluation.
        [Test]
        public void Finding43_Semantics_DeclaredFunctionWithoutInterpretation_IsRejectedByValidate() {
            Assert.That(() => new MissingFunctionSemantics().BuildInterpretation(new Domain(new Element(1))),
                Throws.InvalidOperationException.With.Message.Contains("succ"),
                "a structure total over its signature must interpret every declared function, " +
                "not just the constants");
        }

        // Human holds of every element; Unknown is not in the model at all.
        private static Interpretation HumanEverywhere() {
            IDomainOfDiscourse domain = new Domain(new Element(1), new Element(2));
            var relations = new Dictionary<string, Func<IElementOfDiscourse[], bool>> {
                ["Human"] = _ => true,
                ["Tall"] = args => ((Element)args[0]).Id == 1,
            };
            return new Interpretation(domain, relations,
                new Dictionary<string, Func<IElementOfDiscourse[], IElementOfDiscourse>>(),
                new Dictionary<string, IElementOfDiscourse>(),
                new Dictionary<IProposition, bool>());
        }

        // Finding 44 — universal (free-variable) rules were SILENTLY skipped by Detach: evaluating
        // the antecedent Human(x) with unbound x threw InterpretationException, which Detach's
        // catch misread as "the model doesn't cover this rule's symbols". That contradicts the
        // library-wide convention that free variables are implicitly universal.
        [Test]
        public void Finding44_Detach_UniversalRule_DetachesTheConsequent() {
            var detached = SemanticChaining.Detach(Set("Human(x) => Mortal(x)"), HumanEverywhere());
            Assert.That(detached, Is.EqualTo(Set("Mortal(x)")),
                "the antecedent reads ∀x Human(x), which holds of every element, so the " +
                "consequent detaches; an empty result means the free variable was misread as " +
                "an uncovered symbol");
        }

        [Test]
        public void Finding44_Detach_UniversalRule_FailingOnSomeElement_DoesNotDetach() {
            var detached = SemanticChaining.Detach(Set("Tall(x) => Mortal(x)"), HumanEverywhere());
            Assert.That(detached, Is.Empty,
                "∀x Tall(x) is false (element 2 is not tall), so nothing detaches");
        }

        // The genuine "model doesn't cover these symbols" skip must keep working, and must not
        // turn into a crash now that free variables are closed over.
        [Test]
        public void Finding44_Detach_UncoveredSymbol_IsStillSkippedSilently() {
            var rules = Set("Unknown(x) => Mortal(x)", "Human(x) => Mortal(x)");
            var detached = SemanticChaining.Detach(rules, HumanEverywhere());
            Assert.That(detached, Is.EqualTo(Set("Mortal(x)")),
                "Unknown is not in the model, so its rule is skipped; the covered universal " +
                "rule still detaches");
        }

        [Test]
        public void Finding44_Detach_ExplicitForallRule_AgreesWithTheFreeVariableRule() {
            var free = SemanticChaining.Detach(Set("Human(x) => Mortal(x)"), HumanEverywhere());
            var explicitly = SemanticChaining.Detach(Set("(FORALL x (Human(x))) => Mortal(y)"), HumanEverywhere());
            Assert.That(free, Has.Count.EqualTo(1),
                "free-variable and explicit-FORALL antecedents are the same claim by the " +
                "library's own convention and must detach alike");
            Assert.That(explicitly, Has.Count.EqualTo(1));
        }
    }
}
