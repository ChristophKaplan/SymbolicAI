using System;
using System.Collections.Generic;
using System.Linq;
using FirstOrderLogic;
using NUnit.Framework;

namespace FolTests {
    // Repro tests for the open findings of the July 2026 whole-project reviews. Each test
    // asserts the CORRECT behavior, so a failure confirms the bug is still present.
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
            Assert.That(() => Logic.SkolemForm(sentence), Throws.Nothing);
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
                result = Logic.SkolemForm(sentence);
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
                new Dictionary<string, Func<Term[], IElementOfDiscourse>>(),
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

        // Finding 23 — Unificator.Substitute(Clause) writes substituted literals directly
        // into Clause.Literals, bypassing AddLiteral's dedupe and violating the clause's
        // documented set invariant.
        [Test]
        public void Finding23_UnificatorSubstituteClause_PreservesTheSetInvariant() {
            var clause = new Clause(S("P(x)"), S("P(y)"));
            var unificator = new Unificator(S("R(x,y)"), S("R(a,a)"));

            unificator.Substitute(clause);

            Assert.That(clause.Literals, Has.Count.EqualTo(1),
                "both literals substitute to P(a) and a clause is a set, so in-place " +
                "substitution must not leave duplicate literals behind");
        }

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
    }
}
