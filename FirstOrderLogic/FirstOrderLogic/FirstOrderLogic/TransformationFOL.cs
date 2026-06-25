using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using FirstOrderLogic;

public static class TransformationFOL {
    public enum EquivType {
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

    public static void Transform(EquivType equivType, ref ISentence sentence) {
        sentence = Apply(equivType, sentence);
    }

    private static ISentence Apply(EquivType equivType, ISentence sentence) {
        switch (equivType) {
            case EquivType.SimplifyConstants:         return RewriteBottomUp(sentence, SimplifyConstants);
            case EquivType.DissolveBiconditional:     return RewriteBottomUp(sentence, DissolveBiconditional);
            case EquivType.DissolveImplication:       return RewriteBottomUp(sentence, DissolveImplication);
            case EquivType.PushNegation:              return RewriteBottomUp(sentence, PushNegation);
            case EquivType.DoubleNegation:            return RewriteBottomUp(sentence, DoubleNegation);
            case EquivType.Absorption:                return RewriteBottomUp(sentence, Absorption);
            case EquivType.AssociationAndIdem:        return RewriteBottomUp(sentence, AssociationAndIdem);
            case EquivType.PullQuantifier:            return RewriteBottomUp(sentence, PullQuantifier);
            case EquivType.RemoveDuplicateQuantifier: return RewriteTopDown(sentence, RemoveDuplicateQuantifier);
            case EquivType.RemoveQuantifier:          return RewriteBottomUp(sentence, RemoveQuantifier);
            case EquivType.DistributionOfDisjunction: return RewriteBottomUp(sentence, DistributionOfDisjunction);
            case EquivType.DistributionOfConjunction: return RewriteBottomUp(sentence, DistributionOfConjunction);
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
                    return complex.GetSiblingOf(atomic).Clone();
                case Connective.LogicSymbol.CONJUNCTION when atomic.Contradiction:
                case Connective.LogicSymbol.DISJUNCTION when atomic.Tautology:
                    return atomic.Clone();
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
        var lhsImplication = new ComplexSentence(lhs.Clone(), Connective.LogicSymbol.IMPLICATION, rhs.Clone());
        var rhsImplication = new ComplexSentence(rhs.Clone(), Connective.LogicSymbol.IMPLICATION, lhs.Clone());
        return new ComplexSentence(lhsImplication, Connective.LogicSymbol.CONJUNCTION, rhsImplication);
    }

    private static ISentence DissolveImplication(ISentence sentence) {
        if (sentence is not IComplexSentence complex || complex.Connective != Connective.LogicSymbol.IMPLICATION) {
            return sentence;
        }

        var notLhs = new ComplexSentence(Connective.LogicSymbol.NEGATION, complex.Children[0].Clone());
        return new ComplexSentence(notLhs, Connective.LogicSymbol.DISJUNCTION, complex.Children[1].Clone());
    }

    private static ISentence PushNegation(ISentence sentence) {
        if (sentence is not IComplexSentence { IsNegation: true } negated) {
            return sentence;
        }

        if (negated.Children[0] is IComplexSentence { IsNegation: false, IsQuantifier: true } quantified) {
            var flipped = FlipQuantifier((Quantifier)quantified.Connective);
            return new ComplexSentence(flipped, quantified.Children[0].Negated());
        }

        if (negated.Children[0] is IComplexSentence { IsNegation: false, IsBinary: true } inner) {
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
            return doubleNegation.Children[0].Clone();
        }

        return sentence;
    }

    private static ISentence Absorption(ISentence sentence) {
        if (!sentence.IsBinary) {
            return sentence;
        }

        var complex = (IComplexSentence)sentence;
        var lhs = sentence.Children[0];
        var rhs = sentence.Children[1];

        if (rhs is IComplexSentence rhsComplex && IsDualOperator(complex.Connective, rhsComplex.Connective) && rhsComplex.Children.Contains(lhs)) {
            return lhs.Clone();
        }

        if (lhs is IComplexSentence lhsComplex && IsDualOperator(complex.Connective, lhsComplex.Connective) && lhsComplex.Children.Contains(rhs)) {
            return rhs.Clone();
        }

        return sentence;

        bool IsDualOperator(Connective.LogicSymbol o1, Connective.LogicSymbol o2) {
            switch (o1) {
                case Connective.LogicSymbol.CONJUNCTION when o2 == Connective.LogicSymbol.DISJUNCTION:
                case Connective.LogicSymbol.DISJUNCTION when o2 == Connective.LogicSymbol.CONJUNCTION:
                    return true;
                default:
                    return false;
            }
        }
    }

    private static ISentence AssociationAndIdem(ISentence sentence) {
        if (!sentence.IsBinary) {
            return sentence;
        }

        var complex = (IComplexSentence)sentence;
        var lhs = sentence.Children[0];
        var rhs = sentence.Children[1];

        if ((complex.IsConjunction || complex.IsDisjunction) && lhs.Equals(rhs)) {
            return lhs.Clone();
        }

        if (rhs is IComplexSentence rhsComplex && IsEquivOperator(complex.Connective, rhsComplex.Connective) && rhsComplex.Children.Contains(lhs)) {
            return rhs.Clone();
        }

        if (lhs is IComplexSentence lhsComplex && IsEquivOperator(complex.Connective, lhsComplex.Connective) && lhsComplex.Children.Contains(rhs)) {
            return lhs.Clone();
        }

        return sentence;

        bool IsEquivOperator(Connective.LogicSymbol o1, Connective.LogicSymbol o2) {
            switch (o1) {
                case Connective.LogicSymbol.CONJUNCTION when o2 == Connective.LogicSymbol.CONJUNCTION:
                case Connective.LogicSymbol.DISJUNCTION when o2 == Connective.LogicSymbol.DISJUNCTION:
                    return true;
                default:
                    return false;
            }
        }
    }

    private static ISentence RemoveDuplicateQuantifier(ISentence sentence) {
        if (sentence is IComplexSentence { IsQuantifier: true } quantified &&
            quantified.Children[0] is IComplexSentence { IsQuantifier: true } childQuantified &&
            Equals(quantified.Connective, childQuantified.Connective)) {
            return childQuantified.Clone();
        }

        return sentence;
    }

    private static ISentence RemoveQuantifier(ISentence sentence) {
        return sentence is IComplexSentence { IsQuantifier: true } quantified
            ? quantified.Children[0].Clone()
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
            var newLhs = new ComplexSentence(lhs.Clone(), outer, rhsComplex.Children[0].Clone());
            var newRhs = new ComplexSentence(lhs.Clone(), outer, rhsComplex.Children[1].Clone());
            return new ComplexSentence(newLhs, inner, newRhs);
        }

        if (lhs is IComplexSentence lhsComplex && lhsComplex.Connective.Symbol == inner) {
            var newLhs = new ComplexSentence(lhsComplex.Children[0].Clone(), outer, rhs.Clone());
            var newRhs = new ComplexSentence(lhsComplex.Children[1].Clone(), outer, rhs.Clone());
            return new ComplexSentence(newLhs, inner, newRhs);
        }

        return sentence;
    }

    private static int _captureRenameCounter;

    private static ISentence PullQuantifier(ISentence sentence) {
        if (sentence is not IComplexSentence { IsBinary: true } connective) {
            return sentence;
        }

        foreach (var child in connective.Children) {
            if (child is not IComplexSentence { IsQuantifier: true } quantified) {
                continue;
            }

            var quantifier = (Quantifier)quantified.Connective;
            var sibling = connective.GetSiblingOf(quantified);
            var body = quantified.Children[0];

            // Capture avoidance: pulling widens the quantifier's scope over the sibling, so a free
            // occurrence of the bound name there must be renamed away first.
            if (HasFreeOccurrence(sibling, quantifier.Variable)) {
                var fresh = new Variable($"q${Interlocked.Increment(ref _captureRenameCounter)}");
                body = SubstituteFree(body, quantifier.Variable, fresh);
                quantifier = new Quantifier(quantifier.Symbol, fresh);
            }

            var newBody = new ComplexSentence(body.Clone(), connective.Connective.Symbol, sibling.Clone());
            return new ComplexSentence(quantifier.Clone(), newBody);
        }

        return sentence;
    }

    // True iff `variable` occurs in `sentence` outside the scope of any same-named quantifier.
    private static bool HasFreeOccurrence(ISentence sentence, Variable variable) {
        if (sentence is IComplexSentence { IsQuantifier: true } quantified &&
            ((Quantifier)quantified.Connective).Variable.Equals(variable)) {
            return false;
        }

        if (sentence is IPredicate predicate) {
            return predicate.GetVariables().Contains(variable);
        }

        return sentence.Children.Any(child => HasFreeOccurrence(child, variable));
    }

    // Renames only the free occurrences of `variable`, stopping at same-named binders.
    private static ISentence SubstituteFree(ISentence sentence, Variable variable, Variable replacement) {
        if (sentence is IComplexSentence { IsQuantifier: true } quantified &&
            ((Quantifier)quantified.Connective).Variable.Equals(variable)) {
            return sentence;
        }

        if (sentence is IAtomicSentence) {
            return sentence.Substitute(variable, replacement);
        }

        return sentence.WithChildren(sentence.Children.Select(c => SubstituteFree(c, variable, replacement)).ToList());
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
