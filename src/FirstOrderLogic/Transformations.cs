using System;
using System.Linq;
using System.Threading;

namespace FirstOrderLogic {
    public static class Transformations {
        public enum RewriteRule {
            SimplifyConstants,
            DissolveImplication,
            PushNegation,
            DoubleNegation,
            Absorption,
            AssociationAndIdem,
            DissolveBiconditional,
            PullQuantifier,
            RemoveDuplicateQuantifier,
            RemoveQuantifier,
            DistributionOfDisjunction,
            DistributionOfConjunction
        }

        public static void Transform(RewriteRule rule, ref ISentence sentence) {
            sentence = Apply(rule, sentence);
        }

        private static ISentence Apply(RewriteRule rule, ISentence sentence) {
            switch (rule) {
                case RewriteRule.SimplifyConstants:         return RewriteBottomUp(sentence, SimplifyConstants);
                case RewriteRule.DissolveBiconditional:     return RewriteBottomUp(sentence, DissolveBiconditional);
                case RewriteRule.DissolveImplication:       return RewriteBottomUp(sentence, DissolveImplication);
                case RewriteRule.PushNegation:              return RewriteBottomUp(sentence, PushNegation);
                case RewriteRule.DoubleNegation:            return RewriteBottomUp(sentence, DoubleNegation);
                case RewriteRule.Absorption:                return RewriteBottomUp(sentence, Absorption);
                case RewriteRule.AssociationAndIdem:        return RewriteBottomUp(sentence, AssociationAndIdem);
                case RewriteRule.PullQuantifier:            return RewriteBottomUp(sentence, PullQuantifier);
                case RewriteRule.RemoveDuplicateQuantifier: return RewriteTopDown(sentence, RemoveDuplicateQuantifier);
                case RewriteRule.RemoveQuantifier:          return RewriteBottomUp(sentence, RemoveQuantifier);
                case RewriteRule.DistributionOfDisjunction: return RewriteBottomUp(sentence, DistributionOfDisjunction);
                case RewriteRule.DistributionOfConjunction: return RewriteBottomUp(sentence, DistributionOfConjunction);
                default:                                  return sentence;
            }
        }

        private static ISentence RewriteBottomUp(ISentence sentence, Func<ISentence, ISentence> rule) {
            if (sentence.Children.Count > 0) {
                sentence = sentence.WithChildren(sentence.Children.Select(c => RewriteBottomUp(c, rule)).ToList());
            }

            return rule(sentence);
        }

        private static ISentence RewriteTopDown(ISentence sentence, Func<ISentence, ISentence> rule) {
            sentence = rule(sentence);
            if (sentence.Children.Count > 0) {
                sentence = sentence.WithChildren(sentence.Children.Select(c => RewriteTopDown(c, rule)).ToList());
            }

            return sentence;
        }

        private static ISentence SimplifyConstants(ISentence sentence) {
            if (sentence is not IComplexSentence complex) {
                return sentence;
            }

            if (complex.IsLiteral) {
                return complex.Children[0] is IAtomicSentence { IsNullaryConstant: true } constant
                    ? constant.Negated()
                    : sentence;
            }

            if (complex.Connective != Connective.LogicSymbol.CONJUNCTION &&
                complex.Connective != Connective.LogicSymbol.DISJUNCTION) {
                return sentence;
            }

            foreach (var child in complex.Children) {
                if (child is not IAtomicSentence { IsNullaryConstant: true } atomic) {
                    continue;
                }

                switch (complex.Connective.Symbol) {
                    case Connective.LogicSymbol.CONJUNCTION when atomic.Tautology:
                    case Connective.LogicSymbol.DISJUNCTION when atomic.Contradiction:
                        return complex.GetSiblingOf(atomic);
                    case Connective.LogicSymbol.CONJUNCTION when atomic.Contradiction:
                    case Connective.LogicSymbol.DISJUNCTION when atomic.Tautology:
                        return atomic;
                }
            }

            return sentence;
        }

        private static ISentence DissolveBiconditional(ISentence sentence) {
            if (sentence is not IComplexSentence complex || complex.Connective != Connective.LogicSymbol.BICONDITIONAL) {
                return sentence;
            }

            var lhs = complex.Children[0];
            var rhs = complex.Children[1];
            var lhsImplication = new ComplexSentence(lhs, Connective.LogicSymbol.IMPLICATION, rhs);
            var rhsImplication = new ComplexSentence(rhs, Connective.LogicSymbol.IMPLICATION, lhs);
            return new ComplexSentence(lhsImplication, Connective.LogicSymbol.CONJUNCTION, rhsImplication);
        }

        private static ISentence DissolveImplication(ISentence sentence) {
            if (sentence is not IComplexSentence complex || complex.Connective != Connective.LogicSymbol.IMPLICATION) {
                return sentence;
            }

            var notLhs = new ComplexSentence(Connective.LogicSymbol.NEGATION, complex.Children[0]);
            return new ComplexSentence(notLhs, Connective.LogicSymbol.DISJUNCTION, complex.Children[1]);
        }

        private static ISentence PushNegation(ISentence sentence) {
            if (sentence is not IComplexSentence { IsNegation: true } negated) {
                return sentence;
            }

            if (negated.Children[0] is IComplexSentence { IsNegation: false, IsQuantifier: true } quantified) {
                var flipped = FlipQuantifier((Quantifier)quantified.Connective);
                return new ComplexSentence(flipped, quantified.Children[0].Negated());
            }

            // De Morgan only: ¬(A ⇒ B) and ¬(A ⇔ B) do not distribute this way, so implications
            // and biconditionals stay untouched (the CNF pipeline dissolves them first anyway).
            if (negated.Children[0] is IComplexSentence { IsNegation: false, IsBinary: true } inner &&
                (inner.Connective == Connective.LogicSymbol.CONJUNCTION ||
                 inner.Connective == Connective.LogicSymbol.DISJUNCTION)) {
                return new ComplexSentence(
                    inner.Children[0].Negated(),
                    FlipBinary(inner.Connective.Symbol),
                    inner.Children[1].Negated());
            }

            return sentence;
        }

        private static ISentence DoubleNegation(ISentence sentence) {
            if (sentence is IComplexSentence { IsNegation: true } negation &&
                negation.Children[0] is IComplexSentence { IsNegation: true } doubleNegation) {
                return doubleNegation.Children[0];
            }

            return sentence;
        }

        // Absorption keeps the atom side (a ∧ (a ∨ b) ⇒ a); the same-operator case keeps the
        // compound side ((a ∧ b) ∧ a ⇒ a ∧ b).
        private static ISentence Absorption(ISentence sentence) =>
            CollapseNested(sentence, dual: true, keepCompound: false);

        private static ISentence AssociationAndIdem(ISentence sentence) {
            if (sentence is IComplexSentence complex && sentence.IsBinary &&
                (complex.IsConjunction || complex.IsDisjunction) && sentence.Children[0].Equals(sentence.Children[1])) {
                return sentence.Children[0];
            }

            return CollapseNested(sentence, dual: false, keepCompound: true);
        }

        private static ISentence CollapseNested(ISentence sentence, bool dual, bool keepCompound) {
            if (!sentence.IsBinary) {
                return sentence;
            }

            var complex = (IComplexSentence)sentence;
            var lhs = sentence.Children[0];
            var rhs = sentence.Children[1];

            if (rhs is IComplexSentence rhsComplex && Related(complex.Connective, rhsComplex.Connective) && rhsComplex.Children.Contains(lhs)) {
                return keepCompound ? rhs : lhs;
            }

            if (lhs is IComplexSentence lhsComplex && Related(complex.Connective, lhsComplex.Connective) && lhsComplex.Children.Contains(rhs)) {
                return keepCompound ? lhs : rhs;
            }

            return sentence;

            bool Related(Connective.LogicSymbol o1, Connective.LogicSymbol o2) {
                switch (o1) {
                    case Connective.LogicSymbol.CONJUNCTION:
                        return o2 == (dual ? Connective.LogicSymbol.DISJUNCTION : Connective.LogicSymbol.CONJUNCTION);
                    case Connective.LogicSymbol.DISJUNCTION:
                        return o2 == (dual ? Connective.LogicSymbol.CONJUNCTION : Connective.LogicSymbol.DISJUNCTION);
                    default:
                        return false;
                }
            }
        }

        private static ISentence RemoveDuplicateQuantifier(ISentence sentence) {
            if (sentence is IComplexSentence { IsQuantifier: true } quantified &&
                quantified.Children[0] is IComplexSentence { IsQuantifier: true } childQuantified &&
                Equals(quantified.Connective, childQuantified.Connective)) {
                return childQuantified;
            }

            return sentence;
        }

        private static ISentence RemoveQuantifier(ISentence sentence) {
            return sentence is IComplexSentence { IsQuantifier: true } quantified
                ? quantified.Children[0]
                : sentence;
        }

        private static ISentence DistributionOfDisjunction(ISentence sentence) =>
            Distribute(sentence, Connective.LogicSymbol.DISJUNCTION, Connective.LogicSymbol.CONJUNCTION);

        private static ISentence DistributionOfConjunction(ISentence sentence) =>
            Distribute(sentence, Connective.LogicSymbol.CONJUNCTION, Connective.LogicSymbol.DISJUNCTION);

        private static ISentence Distribute(ISentence sentence, Connective.LogicSymbol outer, Connective.LogicSymbol inner) {
            if (!sentence.IsBinary) {
                return sentence;
            }

            var complex = (IComplexSentence)sentence;
            if (complex.Connective.Symbol != outer) {
                return sentence;
            }

            var lhs = complex.Children[0];
            var rhs = complex.Children[1];

            if (rhs is IComplexSentence rhsComplex && rhsComplex.Connective.Symbol == inner) {
                var newLhs = new ComplexSentence(lhs, outer, rhsComplex.Children[0]);
                var newRhs = new ComplexSentence(lhs, outer, rhsComplex.Children[1]);
                return new ComplexSentence(newLhs, inner, newRhs);
            }

            if (lhs is IComplexSentence lhsComplex && lhsComplex.Connective.Symbol == inner) {
                var newLhs = new ComplexSentence(lhsComplex.Children[0], outer, rhs);
                var newRhs = new ComplexSentence(lhsComplex.Children[1], outer, rhs);
                return new ComplexSentence(newLhs, inner, newRhs);
            }

            return sentence;
        }

        private static int _captureRenameCounter;

        private static ISentence PullQuantifier(ISentence sentence) {
            if (sentence is not IComplexSentence { IsBinary: true } binary) {
                return sentence;
            }

            // Only ∧/∨ preserve the quantifier when its scope widens; pulling out of an implication
            // antecedent would have to flip it (∀x P(x) ⇒ Q is ∃x (P(x) ⇒ Q)), so ⇒/⇔ stay
            // untouched (the prenex pipeline dissolves them first anyway).
            if (binary.Connective != Connective.LogicSymbol.CONJUNCTION &&
                binary.Connective != Connective.LogicSymbol.DISJUNCTION) {
                return sentence;
            }

            foreach (var child in binary.Children) {
                if (child is not IComplexSentence { IsQuantifier: true } quantified) {
                    continue;
                }

                var quantifier = (Quantifier)quantified.Connective;
                var sibling = binary.GetSiblingOf(quantified);
                var body = quantified.Children[0];

                // Capture avoidance: pulling widens the quantifier's scope over the sibling, so the
                // bound name must be renamed away if the sibling uses it at all. A free occurrence
                // there would be captured; a sibling BINDER of the same name would instead leave the
                // prenex prefix shadowed (∀v ∃v …) — still equivalent, since one of the two binders
                // is then vacuous, but rejected by Interpretation.Evaluate as a scope conflict.
                if (OccursIn(sibling, quantifier.Variable)) {
                    var fresh = new Variable($"q${Interlocked.Increment(ref _captureRenameCounter)}");
                    body = body.Substitute(quantifier.Variable, fresh);
                    quantifier = new Quantifier(quantifier.Symbol, fresh);
                }

                var newBody = new ComplexSentence(body, binary.Connective.Symbol, sibling);
                return new ComplexSentence(quantifier.Clone(), newBody);
            }

            return sentence;
        }

        // Any occurrence counts, free or bound: renaming a binder to a fresh name is always
        // equivalence-preserving, so over-renaming only costs an uglier name.
        private static bool OccursIn(ISentence sentence, Variable variable) {
            if (sentence is IComplexSentence { IsQuantifier: true } quantified &&
                ((Quantifier)quantified.Connective).Variable.Equals(variable)) {
                return true;
            }

            if (sentence is IPredicate predicate) {
                return predicate.GetVariables().Contains(variable);
            }

            return sentence.Children.Any(child => OccursIn(child, variable));
        }

        private static Quantifier FlipQuantifier(Quantifier quantifier) {
            var flipped = quantifier.Symbol == Connective.LogicSymbol.UNIVERSAL
                ? Connective.LogicSymbol.EXISTENTIAL
                : Connective.LogicSymbol.UNIVERSAL;
            return new Quantifier(flipped, quantifier.Variable);
        }

        private static Connective.LogicSymbol FlipBinary(Connective.LogicSymbol symbol) =>
            symbol == Connective.LogicSymbol.CONJUNCTION
                ? Connective.LogicSymbol.DISJUNCTION
                : Connective.LogicSymbol.CONJUNCTION;
    }
}
