using System;
using System.Collections.Generic;
using System.Linq;
using FirstOrderLogic;
using NUnit.Framework;

namespace FolTests {
    // Repro tests for the findings of the July 2026 whole-project reviews, all fixed since.
    // Each test asserts the CORRECT behavior and now serves as a regression pin.
    [Category("OpenIssue")]
    public class OpenIssueTests : TestBase {
        private static readonly TimeSpan Bound = TimeSpan.FromSeconds(5);

        // Finding 1 — ForwardChaining.Saturate adds facts unrenamed, so a non-ground fact
        // exposes its variable to the unifier and a later premise match silently overwrites
        // the binding, deriving a fact that is NOT entailed.
        [Test]
        public void Finding01_ForwardChaining_FactVariables_NoUnsoundDerivation() {
            var kb = Set("P(x,x)", "R(x)", "(P(w,a) AND R(b)) => H(w)");
            var entailsHb = RunWithin(Bound, "Entails(H(b))",
                () => ForwardChaining.Entails(kb, S("H(b)")));
            Assert.That(entailsHb, Is.False,
                "H(b) is not entailed: premise 1 binds w to a, so only H(a) follows; " +
                "deriving H(b) means the fact variable binding x->a was overwritten by x->b");
        }

        [Test]
        public void Finding01_ForwardChaining_FactVariables_EntailedFactIsNotLost() {
            var kb = Set("P(x,x)", "R(x)", "(P(w,a) AND R(b)) => H(w)");
            var entailsHa = RunWithin(Bound, "Entails(H(a))",
                () => ForwardChaining.Entails(kb, S("H(a)")));
            Assert.That(entailsHa, Is.True,
                "P(a,a) and R(b) hold (both facts are universally quantified), so H(a) is entailed");
        }

        // Finding 1 (aliasing variant) — two premises matching the same unrenamed fact share
        // its variable, so the closure only contains the diagonal Q(x,x).
        [Test]
        public void Finding01_ForwardChaining_FactVariables_NoCrossPremiseAliasing() {
            var kb = Set("P(x)", "(P(z) AND P(w)) => Q(z,w)");
            var entails = RunWithin(Bound, "Entails(Q(a,b))",
                () => ForwardChaining.Entails(kb, S("Q(a,b)")));
            Assert.That(entails, Is.True,
                "P is universal, so Q(a,b) follows for any pair; only deriving Q(x,x) means " +
                "both premises aliased the same fact variable");
        }

        // Finding 1 (cyclic-substitution variant) — with unrenamed facts this KB built the
        // cyclic substitution {w->x, x->y, y->x} and killed the process with a stack overflow
        // in Substitution.Walk (uncatchable, hence no test existed before the fix).
        [Test]
        public void Finding01_ForwardChaining_FactVariables_NoCyclicSubstitution() {
            var kb = Set("P(x)", "R(y)", "S(x)", "(P(w) AND R(w) AND S(w)) => T(w)");
            var entails = RunWithin(Bound, "Entails(T(a))",
                () => ForwardChaining.Entails(kb, S("T(a)")));
            Assert.That(entails, Is.True, "P, R, and S are universal, so T(a) follows");
        }

        // Finding 4 — IsUnsatisfiable skips SimplifyConstants when the input is already CNF,
        // so TRUE/FALSE are treated as ordinary resolvable atoms.
        [Test]
        public void Finding04_Resolution_FalseConstant_IsUnsatisfiable() {
            var refuted = RunWithin(Bound, "IsUnsatisfiable(FALSE)",
                () => Resolution.IsUnsatisfiable(S("FALSE"), false, 200));
            Assert.That(refuted, Is.True, "the constant FALSE has no model");
        }

        [Test]
        public void Finding04_Resolution_DerivedFalseClause_IsRecognized() {
            var refuted = RunWithin(Bound, "IsUnsatisfiable((P OR FALSE) AND NOT P)",
                () => Resolution.IsUnsatisfiable(S("(P OR FALSE) AND (NOT P)"), false, 200));
            Assert.That(refuted, Is.True,
                "resolving P out leaves the clause {FALSE}, which is the empty clause in disguise");
        }

        [Test]
        public void Finding04_Resolution_TrueConstant_IsAlwaysEntailed() {
            var entailed = RunWithin(Bound, "Resolve(P, TRUE)",
                () => Resolution.Resolve(S("P"), S("TRUE"), false, 200));
            Assert.That(entailed, Is.True, "every knowledge base entails TRUE");
        }

        // Finding 6 — SkolemForm walks the prenex prefix into a Dictionary keyed by variable,
        // so a prefix that rebinds an existential name (legal, and preserved by ToPrenexForm,
        // which only collapses adjacent identical quantifiers) throws ArgumentException.
        [Test]
        public void Finding06_SkolemForm_RepeatedExistentialVariable_DoesNotThrow() {
            var sentence = S("EXISTS x (FORALL y (EXISTS x (P(x))))");
            Assert.That(() => sentence.SkolemForm(), Throws.Nothing);
        }

        // Finding 7 — Skolem witnesses are named sk1, sk2, ... which are parseable identifiers,
        // so a user constant named sk1 can resolve against a fresh witness.
        [Test]
        public void Finding07_SkolemNames_DoNotCollideWithUserSymbols() {
            FirstOrderLogicExtensions.ResetSkolemCounter();
            var theory = new Theory(Set("P(sk1)"));
            var entails = RunWithin(Bound, "Entails(FORALL x P(x))",
                () => theory.Entails(S("FORALL x (P(x))")));
            Assert.That(entails, Is.False,
                "P(sk1) for one constant does not entail FORALL x P(x); a true result means " +
                "the Skolem witness for the negated goal collided with the user constant sk1");
        }

        // Finding 8 — PushNegation guards only on IsBinary and FlipBinary maps every
        // non-conjunction (including =>) to AND, so NOT (A => B) becomes (NOT A) AND (NOT B)
        // instead of A AND (NOT B). Leaving the sentence untouched would also be acceptable.
        [Test]
        public void Finding08_PushNegation_OverImplication_IsEquivalencePreserving() {
            var sentence = S("NOT (A => B)");
            TransformationFOL.Transform(TransformationFOL.EquivType.PushNegation, ref sentence);
            Assert.That(sentence,
                Is.EqualTo(S("A AND (NOT B)")).Or.EqualTo(S("NOT (A => B)")),
                $"NOT (A => B) is equivalent to A AND (NOT B); got: {sentence}");
        }

        // Finding 8 — PullQuantifier keeps the quantifier symbol when pulling out of an
        // implication antecedent, but (FORALL x P(x)) => Q is EXISTS x (P(x) => Q).
        [Test]
        public void Finding08_PullQuantifier_FromAntecedent_FlipsTheQuantifier() {
            var sentence = S("(FORALL x (P(x))) => Q(a)");
            TransformationFOL.Transform(TransformationFOL.EquivType.PullQuantifier, ref sentence);
            Assert.That(sentence,
                Is.EqualTo(S("EXISTS x (P(x) => Q(a))")).Or.EqualTo(S("(FORALL x (P(x))) => Q(a)")),
                $"pulling FORALL out of an antecedent must flip it to EXISTS; got: {sentence}");
        }

        // Finding 11 — when the positive proof of a NAF target exceeds the remaining depth
        // budget, the empty sub-proof enumeration is read as "not derivable" and NAF spuriously
        // succeeds: a wrong "yes" instead of the documented conservative "no".
        [Test]
        public void Finding11_BackwardChaining_DepthCutoff_MustNotInvertNaf() {
            var kb = Set(
                "Thing(a)", "Base(a)",
                "Base(x) => D1(x)", "D1(x) => D2(x)", "D2(x) => D3(x)", "D3(x) => P(x)",
                "(Thing(x) AND NAF P(x)) => Q(x)");
            var goal = S("Q(a)");

            Assert.That(new BackwardChaining().Entails(kb, goal), Is.False,
                "sanity: P(a) is derivable, so NAF P(a) fails and Q(a) is not entailed");
            Assert.That(new BackwardChaining(3).Entails(kb, goal), Is.False,
                "a depth-limited engine may miss an entailment but must not turn the cutoff " +
                "into a spurious NAF success");
        }

        // Finding 12 — Unificator.GetHashCode iterates dictionary insertion order and Equals
        // short-circuits on the hash, so content-equal unifiers built in different orders
        // compare unequal and are not deduplicated in hash sets.
        [Test]
        public void Finding12_Unificator_ContentEqualBindings_AreEqualRegardlessOfOrder() {
            var u1 = new Unificator(new Dictionary<Variable, Term> {
                { new Variable("x"), new Constant("a") },
                { new Variable("y"), new Constant("b") },
            });
            var u2 = new Unificator(new Dictionary<Variable, Term> {
                { new Variable("y"), new Constant("b") },
                { new Variable("x"), new Constant("a") },
            });

            Assert.That(u1, Is.EqualTo(u2), "same substitution content must compare equal");
            Assert.That(new HashSet<Unificator> { u1, u2 }, Has.Count.EqualTo(1),
                "content-equal unifiers must collapse to one hash-set entry");
        }

        // Finding 13 — SkolemForm draws witness arguments only from the explicit prenex
        // prefix, so a free (implicitly universal) variable never enters the witness and
        // EXISTS y Q(x,y) skolemizes to a single shared constant Q(x, sk$1) instead of
        // Q(x, sk$1(x)) — resolution then proves conclusions that are not entailed.
        [Test]
        public void Finding13_SkolemWitness_MustDependOnImplicitlyUniversalFreeVariables() {
            var entails = RunWithin(Bound, "Resolve(EXISTS y Q(x,y) ⊨ goal)",
                () => Resolution.Resolve(S("EXISTS y (Q(x,y))"), S("EXISTS y (Q(a,y) AND Q(b,y))"), false, 200));
            Assert.That(entails, Is.False,
                "∀x∃y Q(x,y) does not entail ∃y (Q(a,y) AND Q(b,y)); a true result means the " +
                "Skolem witness ignored the free x and became one constant shared by a and b");
        }

        [Test]
        public void Finding13_ExplicitForall_ControlCase_IsNotEntailedEither() {
            var entails = RunWithin(Bound, "Resolve(FORALL x EXISTS y Q(x,y) ⊨ goal)",
                () => Resolution.Resolve(S("FORALL x (EXISTS y (Q(x,y)))"), S("EXISTS y (Q(a,y) AND Q(b,y))"), false, 200));
            Assert.That(entails, Is.False,
                "free-variable and explicit-FORALL KBs are the same sentence by the engine's " +
                "own convention and must resolve identically");
        }

        // Finding 14 — AbductiveChaining's consistency filter uses the exact-match Conflicts()
        // check, so a derived ground negation (NOT Sunny(a)) never conflicts with a universal
        // fact (Sunny(x)) and an inconsistent assumption survives as an explanation.
        [Test]
        public void Finding14_Abduction_AssumptionConflictingWithUniversalFact_IsDiscarded() {
            var explanations = new AbductiveChaining().Explain(
                Set("Sunny(x)", "Rain(z) => NOT Sunny(z)", "Rain(z) => Wet(z)"),
                S("Wet(a)"), new[] { "Rain" });
            Assert.That(explanations, Is.Empty,
                "assuming Rain(a) derives NOT Sunny(a), which contradicts the universal fact " +
                "Sunny(x) — the documented 'without introducing new conflicts' contract");
        }

        // Finding 15 — Theory.RulesMatch compares only Premises and Head; NafPremises never
        // enter the comparison, so rules that fire under different NAF guards (or none at all)
        // count as the same commitment.
        [Test]
        public void Finding15_TheoryCompare_RulesWithDifferentNafGuards_DoNotAgree() {
            var viaQ = new Theory(Set("(P(x) AND (NAF Q(x))) => R(x)"));
            var viaT = new Theory(Set("(P(x) AND (NAF T(x))) => R(x)"));
            Assert.That(viaQ.Compare(viaT).Agreements, Is.Empty,
                "the rules fire under different NAF guards (Q underivable vs T underivable) " +
                "and derive different closures from the same facts");
        }

        [Test]
        public void Finding15_TheoryCompare_NafGuardedRule_IsNotTheStrictRule() {
            var guarded = new Theory(Set("(P(x) AND (NAF Q(x))) => R(x)"));
            var strict = new Theory(Set("P(x) => R(x)"));
            Assert.That(guarded.Compare(strict).Agreements, Is.Empty,
                "a default rule (R unless Q is derivable) and a strict rule are different " +
                "commitments; agreement means the NAF premise was ignored");
        }

        // Finding 16 — SkolemForm's bottom-up RemoveQuantifier rewrite deletes EVERY
        // quantifier, not just the skolemized prefix, so a non-PNF input has its inner
        // existentials silently flipped into free (effectively universal) variables.
        [Test]
        public void Finding16_SkolemForm_NonPrenexInput_MustNotSilentlyDropQuantifiers() {
            var sentence = S("FORALL x (P(x) AND (EXISTS y (Q(y))))");
            ISentence result;
            try {
                result = sentence.SkolemForm();
            } catch (Exception) {
                return; // rejecting non-PNF input outright is also acceptable
            }
            Assert.That(result.ToString(), Does.Contain("sk$"),
                $"the inner EXISTS y must become a Skolem witness, not a free variable — got: {result}");
        }

        // Finding 18 — InstantiateVariable writes its synthetic per-element constants into
        // the interpretation's function table and never removes them, so a logically
        // read-only Evaluate mutates the interpretation and later lookups resolve spuriously.
        [Test]
        public void Finding18_QuantifierEvaluation_MustNotLeakSyntheticConstants() {
            IDomainOfDiscourse domain = new Domain(new Element(1), new Element(2));
            var relations = new Dictionary<string, Func<IElementOfDiscourse[], bool>> {
                ["Human"] = terms => terms[0] is Element e && e.Id == 1,
            };
            var interpretation = new Interpretation(domain, relations,
                new Dictionary<string, Func<IElementOfDiscourse[], IElementOfDiscourse>>(),
                new Dictionary<string, IElementOfDiscourse>(),
                new Dictionary<IProposition, bool>());

            Assert.That(interpretation.Evaluate(S("EXISTS x (Human(x))")), Is.True, "sanity");
            Assert.That(() => interpretation.EvaluateTerm(new Constant("x_element_1")),
                Throws.TypeOf<InterpretationException>(),
                "the synthetic constant used to range over the domain must not remain " +
                "resolvable after the quantifier evaluation returns");
        }

        // Finding 19 — an empty clause is never satisfiable, so it is always eligible for the
        // random-walk branch, where flipping one of its zero literals throws instead of the
        // solver reporting unsat.
        [Test]
        public void Finding19_WalkSat_EmptyClause_ReportsUnsatInsteadOfCrashing() {
            var clauses = new List<Clause> { new Clause() };
            PossibleWorld? model = null;
            Assert.That(() => model = new SatSolvers().WalkSAT(clauses, 1f, 100), Throws.Nothing,
                "the empty clause is a legal Clause value and must not crash the walk");
            Assert.That(model, Is.Null, "no assignment satisfies the empty clause");
        }

        // Finding 20 — resolved as a convention, not a defect: free variables in a GOAL are
        // query variables (AIMA ASK), while KB sentences keep the universal reading
        // (Finding13), so Q(x) and FORALL x (Q(x)) are different questions.
        [Test]
        public void Finding20_Resolution_FreeVariableGoal_IsAnExistentialQuery() {
            var entails = RunWithin(Bound, "Resolve(Q(b) ⊨ Q(x))",
                () => Resolution.Resolve(S("Q(b)"), S("Q(x)"), false, 200));
            Assert.That(entails, Is.True,
                "the query 'does Q hold for some x?' is answered by the instance Q(b)");
        }

        [Test]
        public void Finding20_Resolution_ExplicitForallGoal_IsUniversal() {
            var entails = RunWithin(Bound, "Resolve(Q(b) ⊨ FORALL x Q(x))",
                () => Resolution.Resolve(S("Q(b)"), S("FORALL x (Q(x))"), false, 200));
            Assert.That(entails, Is.False,
                "one instance does not entail the universal claim");
        }

        // Finding 21 — the NAF sub-proof in backward chaining runs against the KB only,
        // ignoring literals already abduced on the same proof path, so an abductive proof can
        // simultaneously assume P(a) and rely on NAF P(a) — yielding an "explanation" that
        // does not actually derive the observation.
        [Test]
        public void Finding21_Abduction_EveryExplanationDerivesTheObservation() {
            var kb = Set("T(a)", "(P(x) AND Q(x)) => W(x)", "(T(x) AND (NAF P(x))) => Q(x)");
            var observation = S("W(a)");

            var explanations = new AbductiveChaining().Explain(kb, observation, new[] { "P" });

            foreach (var explanation in explanations) {
                var augmented = kb.Concat(explanation).ToList();
                var derives = RunWithin(Bound, "Entails(W(a)) under explanation",
                    () => ForwardChaining.Entails(augmented, observation));
                Assert.That(derives, Is.True,
                    $"explanation {{{string.Join(", ", explanation)}}} does not derive " +
                    "W(a): assuming P(a) defeats the NAF P(a) premise the proof itself used");
            }
        }

        // Finding 22 — when a rule premise matches a non-ground (universal) fact, the NAF
        // check runs with the NAF variable still bound to the fact's variable, so one
        // derivable instance vetoes the whole universal match instead of only its own
        // instance, dropping entailed facts (and contradicting backward chaining).
        [Test]
        public void Finding22_ForwardChaining_NafOnUniversalMatch_ChecksPerInstance() {
            var kb = Set("A(x)", "B(a)", "(A(w) AND (NAF B(w))) => C(w)");
            var entails = RunWithin(Bound, "Entails(C(b))",
                () => ForwardChaining.Entails(kb, S("C(b)")));
            Assert.That(entails, Is.True,
                "A(b) holds (A is universal) and B(b) is not derivable, so C(b) follows " +
                "under NAF; a false result means the derivable instance B(a) vetoed the " +
                "whole universal match — BackwardChaining answers true on the same KB");
        }

        // Finding 23 — Unificator.Substitute(Clause) mutated a hash-relevant Clause in place,
        // bypassing AddLiteral's dedupe. The method had no production caller and was deleted;
        // Clause is immutable since Finding 33, so the invariant it broke is now structural.

        // Finding 24 — the public ComplexSentence(LogicSymbol, ISentence) constructor accepts
        // UNIVERSAL/EXISTENTIAL but builds a plain Connective, not a Quantifier; every
        // consumer downcasts, so the node crashes with InvalidCastException on first use.
        [Test]
        public void Finding24_UnaryQuantifierConstructor_YieldsAUsableNodeOrRejects() {
            ComplexSentence sentence;
            try {
                sentence = new ComplexSentence(Connective.LogicSymbol.UNIVERSAL, S("P(x)"));
            } catch (Exception) {
                return; // rejecting the quantifier symbol at construction is also acceptable
            }
            Assert.That(() => sentence.HasScopeConflict(), Throws.Nothing,
                "a node the constructor accepted must be usable: IsQuantifier is true but " +
                "the Connective is not a Quantifier, so consumers' downcasts crash");
        }

        // Finding 24 (Connective overload) — the same corrupt node, built the other way: a plain
        // Connective carrying a quantifier symbol bypassed the guard on the LogicSymbol overload.
        [Test]
        public void Finding24_ConnectiveQuantifierConstructor_YieldsAUsableNodeOrRejects() {
            ComplexSentence sentence;
            try {
                sentence = new ComplexSentence(new Connective(Connective.LogicSymbol.UNIVERSAL), S("P(x)"));
            } catch (ArgumentException) {
                return; // rejecting a quantifier symbol without its bound variable is the fix
            }
            Assert.That(() => sentence.HasScopeConflict(), Throws.Nothing,
                "IsQuantifier is true, so HasScopeConflict downcasts the Connective to Quantifier");
            Assert.That(() => sentence.Substitute(new Variable("x"), new Constant("a")), Throws.Nothing,
                "Substitute performs the same downcast");
        }

        // A Quantifier connective carries the bound variable and must keep working.
        [Test]
        public void Finding24_ConnectiveConstructor_AcceptsARealQuantifier() {
            var sentence = new ComplexSentence(
                new Quantifier(Connective.LogicSymbol.UNIVERSAL, new Variable("x")), S("P(x)"));
            Assert.That(sentence, Is.EqualTo(S("FORALL x (P(x))")));
        }

        // Finding 25 — Inconsistencies()/IsConsistent() feed the theory through forward
        // chaining, whose Rule.FromAll silently drops every sentence outside the literal/rule
        // fragment, so plainly inconsistent theories report consistent.
        [Test]
        public void Finding25_TheoryConsistency_SeesBeyondTheRuleFragment() {
            var theory = new Theory(Set("P(a) AND (NOT P(a))"));
            bool consistent;
            try {
                consistent = theory.IsConsistent();
            } catch (Exception) {
                return; // rejecting sentences the syntactic check cannot analyze is acceptable
            }
            Assert.That(consistent, Is.False,
                "P(a) AND (NOT P(a)) is a contradiction; reporting consistent means the " +
                "conjunction was silently dropped on the way into forward chaining");
        }

        // Finding 26 — the public ISentence.Substitute is raw (not capture-avoiding) under
        // quantifiers, while the capture-safe variant is private to TransformationFOL.
        [Test]
        public void Finding26_PublicSubstitute_UnderAQuantifier_IsCaptureAvoiding() {
            var result = S("FORALL x (P(x,y))").Substitute(new Variable("y"), new Variable("x"));
            Assert.That(result, Is.Not.EqualTo(S("FORALL x (P(x,x))")),
                "substituting x for the free y under FORALL x captures the variable and " +
                "changes the meaning to the diagonal; the binder must be renamed first " +
                "(or the call rejected)");
        }

        // Finding 27 — Element carries an Id but no Equals/GetHashCode, so two Element(1)
        // instances are unequal and equality- or set-based relation definitions silently fail.
        [Test]
        public void Finding27_Element_EqualityIsById() {
            var first = new Element(1);
            var second = new Element(1);
            Assert.That(first, Is.EqualTo(second),
                "Element is defined by its Id, so equal Ids must denote the same individual; " +
                "reference equality silently breaks equality-based relation definitions");
        }

        // Finding 28 — BackwardChaining's NAF commits to the "no instance derivable" reading
        // for any variable still free at NAF time, including genuine existential query
        // variables, so one derivable instance vetoes every instance instead of only its own
        // (ForwardChaining fixed the same defect as Finding 22).
        [Test]
        public void Finding28_BackwardChaining_NafFreeQueryVariable_SomeInstanceReading() {
            var kb = Set("A(z)", "B(e)", "F(d)", "(A(x) AND (NAF B(x))) => C(x)");
            var entailsGround = RunWithin(Bound, "Entails(C(d))",
                () => new BackwardChaining().Entails(kb, S("C(d)")));
            Assert.That(entailsGround, Is.True,
                "sanity: A(d) holds (A is universal) and B(d) is not derivable, so C(d) follows");
            var entailsFree = RunWithin(Bound, "Entails(C(w))",
                () => new BackwardChaining().Entails(kb, S("C(w)")));
            Assert.That(entailsFree, Is.True,
                "a free query variable asks for SOME instance and C(d) is a witness; a false " +
                "result means the derivable instance B(e) vetoed the whole NAF instead of " +
                "only its own instance");
        }

        [Test]
        public void Finding28_BackwardChaining_NafThroughSecondRule_AgreesWithForwardChaining() {
            var kb = Set("A(z)", "D(d)", "B(e)",
                "(A(x) AND (NAF B(x))) => C(x)", "(C(y) AND D(y)) => E(y)");
            var fc = RunWithin(Bound, "FC.Entails(E(w))",
                () => ForwardChaining.Entails(kb, S("E(w)")));
            Assert.That(fc, Is.True, "sanity: forward chaining derives E(d) via C(d)");
            var bc = RunWithin(Bound, "BC.Entails(E(w))",
                () => new BackwardChaining().Entails(kb, S("E(w)")));
            Assert.That(bc, Is.True,
                "C(d) holds under per-instance NAF and D(d) is a fact, so E(d) witnesses E(w)");
        }

        [Test]
        public void Finding28_BackwardChaining_PremiseOrder_MustNotChangeTheAnswer() {
            var kb = Set("A(z)", "D(d)", "B(e)",
                "(A(x) AND (NAF B(x))) => C(x)", "(D(y) AND C(y)) => E(y)");
            var bc = RunWithin(Bound, "BC.Entails(E(w)) with swapped premises",
                () => new BackwardChaining().Entails(kb, S("E(w)")));
            Assert.That(bc, Is.True,
                "premise order carries no logical meaning: (D(y) AND C(y)) must answer like " +
                "(C(y) AND D(y))");
        }

        // Finding 29 — Substitution.Apply is a silent no-op on NAF-wrapped goals (AtomOf
        // returned the NAF wrapper itself, hiding the variables), so backward chaining matched
        // assumptions against, and recorded into `denied`, the UNsubstituted NAF target, whose
        // free variable over-matches every assumption and kills valid abductive explanations.
        [Test]
        public void Finding29_Abduction_NafTargetIsSubstituted_ValidExplanationSurvives() {
            var kb = Set("T(b)", "(P(a) AND Q(b)) => W(a)", "(T(x) AND (NAF P(x))) => Q(x)");
            var explanations = new AbductiveChaining().Explain(kb, S("W(a)"), new[] { "P" });
            Assert.That(explanations, Has.Count.EqualTo(1),
                "assuming P(a) proves W(a): Q(b) follows from T(b) and NAF P(b), and P(b) is " +
                "neither derivable nor assumed — an empty result means the unsubstituted NAF " +
                "target P(x) over-matched the assumption P(a)");
            Assert.That(explanations[0].Single().ToString(), Is.EqualTo(S("P(a)").ToString()));
        }

        [Test]
        public void Finding29_Abduction_PremiseOrder_MustNotChangeTheExplanation() {
            var kb = Set("T(b)", "(Q(b) AND P(a)) => W(a)", "(T(x) AND (NAF P(x))) => Q(x)");
            var explanations = new AbductiveChaining().Explain(kb, S("W(a)"), new[] { "P" });
            Assert.That(explanations, Has.Count.EqualTo(1),
                "with Q(b) proven first, P(b) enters `denied`; assuming P(a) afterwards must " +
                "still be allowed — only P(b) itself would defeat the recorded NAF failure");
            Assert.That(explanations[0].Single().ToString(), Is.EqualTo(S("P(a)").ToString()));
        }

        [Test]
        public void Finding29_Abduction_GroundNafControlCase() {
            var kb = Set("T(b)", "(P(a) AND Q(b)) => W(a)", "(T(b) AND (NAF P(b))) => Q(b)");
            var explanations = new AbductiveChaining().Explain(kb, S("W(a)"), new[] { "P" });
            Assert.That(explanations, Has.Count.EqualTo(1));
            Assert.That(explanations[0].Single().ToString(), Is.EqualTo(S("P(a)").ToString()));
        }

        // Finding 30 — KernelSets and Theory ran Resolution with maxRounds = 0 (unlimited);
        // kernel shrinking inherently asks non-entailed questions, so a term-generating rule
        // (P(x) => P(f(x))) made FindKernel hang forever. With the finite default it must
        // terminate: either with the kernel or with Resolution's InvalidOperationException.
        [Test]
        public void Finding30_KernelSets_TermGeneratingRule_MustNotHang() {
            var beliefs = Set("P(a)", "P(x) => P(f(x))", "Q(c)");
            List<ISentence>? kernel = null;
            var undecided = false;
            RunWithin(Bound, "FindKernel", () => {
                try {
                    kernel = new KernelSets().FindKernel(beliefs, S("Q(c)"));
                } catch (InvalidOperationException) {
                    undecided = true;
                }
            });
            if (!undecided) {
                Assert.That(kernel, Is.Not.Null, "sanity: the belief base entails Q(c)");
                Assert.That(kernel, Is.EqualTo(new List<ISentence> { S("Q(c)") }),
                    "Q(c) is the only load-bearing belief for Q(c)");
            }
        }

        [Test]
        public void Finding30_TheoryEntails_MaxRounds_FailsLoudlyInsteadOfHanging() {
            var theory = new Theory(Set("P(a)", "P(x) => P(f(x))"));
            RunWithin(Bound, "Entails(Q(c), maxRounds: 50)", () => {
                Assert.That(() => theory.Entails(S("Q(c)"), maxRounds: 50),
                    Throws.InvalidOperationException,
                    "the rule mints P(f(a)), P(f(f(a))), … forever, so a bounded run can only " +
                    "end in the loud undecided exception");
            });
        }

        // Finding 31 — Holds/Answers unify the query directly against facts without
        // standardizing apart, so a variable name shared between query and a non-ground fact
        // wrongly fails unification; Answers additionally returned bindings keyed on the
        // fact's internal variable instead of projecting onto the query's variables.
        [Test]
        public void Finding31_ForwardChainingHolds_SharedVariableName_StillUnifies() {
            var facts = Set("Parent(Anna, x)");
            Assert.That(ForwardChaining.Holds(facts, S("Parent(x, Bob)")), Is.True,
                "the fact's x and the query's x are different variables (the fact is " +
                "universally quantified); renaming the fact variable to y makes this true, " +
                "so the shared name must not block unification");
        }

        [Test]
        public void Finding31_ForwardChainingAnswers_ProjectsOntoQueryVariables() {
            var facts = ForwardChaining.Saturate(Set("P(x)"));
            var answers = ForwardChaining.Answers(facts, S("P(c)"));
            Assert.That(answers, Has.Count.EqualTo(1), "the universal fact P(x) covers P(c)");
            Assert.That(answers[0], Is.Empty,
                "a ground query has no variables to bind; a non-empty binding set means the " +
                "fact's internal variable leaked out instead of being projected away");
        }

        // Finding 32 — the head check rejected ANY compound term including ground ones, but a
        // ground head like Q(f(a)) mints no fresh terms (saturation terminates) and ground
        // compound facts are already accepted, so the rejection was inconsistent.
        [Test]
        public void Finding32_ForwardChaining_GroundCompoundHead_IsAccepted() {
            var closure = RunWithin(Bound, "Saturate",
                () => ForwardChaining.Saturate(Set("R(a)", "R(x) => Q(f(a))")));
            Assert.That(closure, Is.EquivalentTo(Set("R(a)", "Q(f(a))")),
                "a ground compound head derives exactly one new fact");
        }

        [Test]
        public void Finding32_ForwardChaining_VariableCompoundHead_IsStillRejected() {
            Assert.That(() => ForwardChaining.Saturate(Set("P(a)", "P(x) => P(f(x))")),
                Throws.ArgumentException,
                "a head function term over variables mints fresh terms every round and must " +
                "keep being rejected");
        }

        // Finding 33 — Clause was mutable while serving as a hash-set key in Resolution's `seen`
        // set: AddLiteral (and the exposed List) let a stored clause change, stranding it in its
        // old bucket, and Equals compared containment one way only, so a duplicate smuggled past
        // the constructor's dedupe made c1.Equals(c2) true while c2.Equals(c1) was false.
        [Test]
        public void Finding33_Clause_IsNotMutatedWhileStoredInAHashSet() {
            var clause = new Clause(S("P(a)"));
            var seen = new HashSet<Clause> { clause };

            var extended = clause.With(S("Q(a)"));

            Assert.That(seen.Contains(clause), Is.True,
                "extending a clause must not change the one already used as a key");
            Assert.That(clause.Literals, Has.Count.EqualTo(1), "the original is untouched");
            Assert.That(extended.Literals, Has.Count.EqualTo(2), "the extension is a new clause");
        }

        [Test]
        public void Finding33_Clause_EqualityIsSymmetricAndOrderInsensitive() {
            var pq = new Clause(S("P(a)"), S("Q(a)"));
            var qp = new Clause(S("Q(a)"), S("P(a)"));
            var p = new Clause(S("P(a)"));

            Assert.That(pq, Is.EqualTo(qp), "a clause is a set, so literal order cannot matter");
            Assert.That(qp, Is.EqualTo(pq));
            Assert.That(pq.GetHashCode(), Is.EqualTo(qp.GetHashCode()));
            Assert.That(new HashSet<Clause> { pq, qp }, Has.Count.EqualTo(1));

            Assert.That(p.Equals(pq), Is.EqualTo(pq.Equals(p)), "equality must be symmetric");
            Assert.That(p.Equals(pq), Is.False, "{P(a)} and {P(a), Q(a)} are different clauses");
        }

        // Finding 34 — ToConjunctiveNormalForm prenexed but never stripped the quantifier prefix,
        // and IsCNF() is false for any quantifier node, so EVERY quantified sentence did the full
        // prenex work and then died on a misleading "Sentence is not in CNF".
        [Test]
        public void Finding34_Cnf_UniversallyQuantifiedInput_Converts() {
            var cnf = S("FORALL x (P(x) OR (Q(x) AND R(x)))").ToConjunctiveNormalForm();
            Assert.That(cnf.IsCNF(), Is.True);
            Assert.That(cnf, Is.EqualTo(S("(P(x) OR Q(x)) AND (P(x) OR R(x))")),
                "free variables are implicitly universal in this library, so dropping a " +
                "universal prefix is equivalence-preserving");
        }

        [Test]
        public void Finding34_Cnf_PlainUniversal_Converts() =>
            Assert.That(S("FORALL x (P(x))").ToConjunctiveNormalForm(), Is.EqualTo(S("P(x)")));

        [Test]
        public void Finding34_Cnf_ExistentialInput_ThrowsAndNamesSkolemForm() =>
            Assert.That(() => S("EXISTS x (P(x))").ToConjunctiveNormalForm(),
                Throws.TypeOf<ExistentialQuantifierException>().And.Message.Contains("SkolemForm"),
                "eliminating an existential only preserves satisfiability, so an " +
                "equivalence-preserving CNF must refuse and point at the explicit pre-step");

        [Test]
        public void Finding34_Cnf_SkolemFormFirst_IsTheDocumentedRoute() {
            var cnf = S("EXISTS x (P(x) OR (Q(x) AND R(x)))").SkolemForm().ToConjunctiveNormalForm();
            Assert.That(cnf.IsCNF(), Is.True);
        }

        [Test]
        public void Finding34_Cnf_QuantifierFreeInput_IsUnchanged() =>
            Assert.That(S("A OR (B AND C)").ToConjunctiveNormalForm(),
                Is.EqualTo(S("(A OR B) AND (A OR C)")),
                "the previously working quantifier-free path must keep its behaviour");

        // Finding 35 — Substitute's capture handling special-cased only a target EQUAL to the
        // bound variable, so a compound target merely containing it consumed bound occurrences:
        // (FORALL x P(f(x))).Substitute(f(x), a) silently returned the different sentence ∀x P(a).
        [Test]
        public void Finding35_Substitute_CompoundTargetContainingABoundVariable_IsNotApplied() {
            var sentence = S("FORALL x (P(f(x)))");
            var fx = new Function("f", new Term[] { new Variable("x") });

            Assert.That(sentence.Substitute(fx, new Constant("a")), Is.EqualTo(sentence),
                "f(x)'s x is bound here, so the scope holds no free occurrence of f(x) to replace");
        }

        [Test]
        public void Finding35_Substitute_CompoundTargetOverAFreeVariable_StillApplies() {
            var fy = new Function("f", new Term[] { new Variable("y") });

            Assert.That(S("FORALL x (P(f(y)))").Substitute(fy, new Constant("a")),
                Is.EqualTo(S("FORALL x (P(a))")),
                "y is free under the binder, so compound-target substitution must still work");
        }

        // Finding 36 — GetLiterals recursed through negations of complex children and NAF nodes
        // and handed back their atoms stripped of the polarity that gives them their meaning:
        // NOT (P(a) OR Q(a)) yielded [P(a), Q(a)], as did (NAF P(a)) AND Q(a).
        [Test]
        public void Finding36_GetLiterals_RejectsInputItCannotAnswerFor() {
            Assert.That(() => S("NOT (P(a) OR Q(a))").GetLiterals(),
                Throws.InvalidOperationException,
                "returning [P(a), Q(a)] here inverts the polarity of both literals");
            Assert.That(() => S("(NAF P(a)) AND Q(a)").GetLiterals(),
                Throws.InvalidOperationException,
                "returning P(a) drops the NAF wrapper, which is the whole meaning of the node");
        }

        [Test]
        public void Finding36_GetLiterals_ClauseInput_StillWorks() =>
            Assert.That(S("A OR (NOT B) OR C").GetLiterals(), Has.Count.EqualTo(3),
                "a disjunction of literals is exactly what GetLiterals is for");

        // Finding 37 — IsNegationOf(other, onlyPredSignature: true) called GetPredicate() on
        // whichever side was not a proposition, and GetAtom throws for a complex sentence, so the
        // boolean predicate threw over half its input space instead of answering false.
        [Test]
        public void Finding37_IsNegationOf_PredSignature_NonLiteralComparand_IsFalse() {
            Assert.That(S("NOT P(a)").IsNegationOf(S("Q(b) AND R(c)"), onlyPredSignature: true),
                Is.False, "a conjunction is not the negation of a literal — and asking must not throw");
            Assert.That(S("NOT (P(a) AND Q(a))").IsNegationOf(S("P(a)"), onlyPredSignature: true),
                Is.False, "the negated side is complex, so there is no predicate signature to compare");
        }

        // Finding 38 — Signature.Collect only flagged IPredicate atoms, so a bare proposition
        // sailed through validation on an EMPTY signature; since the parser turns any identifier
        // without parentheses into a Proposition, that is exactly the misspelling class Covers
        // exists to catch. Ground, meanwhile, always built Constants although the parser reads
        // x/y/z/w as Variables, so Ground-built atoms were not Equals-identical to parsed ones
        // and Theory.Holds silently answered false.
        [Test]
        public void Finding38_Signature_BarePropositionsAreValidated() {
            Assert.That(new Signature.Builder().Build().Covers(S("A")), Is.False,
                "an empty signature declares no symbol at all, so it cannot cover A/0");
            Assert.That(new Signature.Builder().Build().UndeclaredPredicates(S("A")),
                Does.Contain("A/0"));
            Assert.That(new Signature.Builder().Predicate("A", 0).Build().Covers(S("A")), Is.True,
                "a declared 0-ary predicate covers the proposition A");
        }

        [Test]
        public void Finding38_Signature_LogicalConstantsAreNotSignatureSymbols() =>
            Assert.That(new Signature.Builder().Build().Covers(S("TRUE")), Is.True,
                "TRUE/FALSE are truth values, not symbols a signature could declare");

        [Test]
        public void Finding38_SignatureGround_MatchesTheParsersVariableWhitelist() {
            Assert.That((ISentence)new Signature.Symbol("P", 1).Ground("x"), Is.EqualTo(S("P(x)")),
                "the parser reads x as a Variable, and Ground must agree or structural " +
                "equality between the two construction paths breaks");
            Assert.That((ISentence)new Signature.Symbol("P", 1).Ground("a"), Is.EqualTo(S("P(a)")),
                "non-whitelisted names stay Constants");
        }

        // Finding 39 — the NAF guard was applied inconsistently: SkolemForm happily skolemized
        // under an operator the library declares classically meaningless, and Conflicts picked a
        // NAF node as the "positive" partner of a negation and fed it to the unifier, which
        // rejects non-literals with a bare Exception.
        [Test]
        public void Finding39_SkolemForm_RejectsNaf_LikeItsSiblings() =>
            Assert.That(() => S("EXISTS x (NAF P(x))").SkolemForm(), Throws.ArgumentException,
                "CNF and GetClauseSet reject NAF as having no classical semantics; skolemizing " +
                "under it must not be the one way in");

        [Test]
        public void Finding39_Conflicts_NafMixedWithANegation_DoesNotCrash() {
            var literals = Set("NAF P(a)", "NOT P(a)");
            List<ISentence>? conflicts = null;

            Assert.That(() => conflicts = literals.Conflicts(), Throws.Nothing,
                "NAF P(a) is not a classical literal, so it cannot be the positive partner of " +
                "NOT P(a) — selecting it fed a non-literal to the unifier");
            Assert.That(conflicts, Is.Empty,
                "'not derivable' contradicts nothing classically, so there is no conflict here");
        }

        // Finding 45 — Predicate and Function retained the caller's Term[] and handed it straight
        // back out, while Equals/GetHashCode read it: mutating the array behind a constructed atom
        // changed a sentence a hash set had already bucketed, stranding it in the wrong bucket.
        [Test]
        public void Finding45_Predicate_CallerMutatingItsTermArray_CannotAffectTheAtom() {
            var terms = new Term[] { new Constant("a") };
            var p = new Predicate("P", terms);
            var set = new HashSet<ISentence> { p };

            terms[0] = new Constant("b");

            Assert.That(p.ToString(), Is.EqualTo("P(a)"),
                "the atom was built over a, so it must still read P(a)");
            Assert.That(set.Contains(p), Is.True,
                "a constructed atom must not be able to change identity underneath its hash set");
            Assert.That((ISentence)p, Is.EqualTo(S("P(a)")));
        }

        [Test]
        public void Finding45_Function_CallerMutatingItsTermArray_CannotAffectTheTerm() {
            var terms = new Term[] { new Constant("a") };
            var f = new Function("f", terms);
            var hashBefore = f.GetHashCode();

            terms[0] = new Constant("b");

            Assert.That(f.ToString(), Is.EqualTo("f(a)"));
            Assert.That(f.GetHashCode(), Is.EqualTo(hashBefore));
            Assert.That(f, Is.EqualTo(new Function("f", new Term[] { new Constant("a") })));
        }

        // Signature.Symbol.Applied forwarded the caller's array straight into that trap.
        [Test]
        public void Finding45_SignatureApplied_DoesNotAliasTheCallersArray() {
            var terms = new Term[] { new Constant("a") };
            var p = new Signature.Symbol("P", 1).Applied(terms);

            terms[0] = new Constant("b");

            Assert.That((ISentence)p, Is.EqualTo(S("P(a)")));
        }
    }
}
