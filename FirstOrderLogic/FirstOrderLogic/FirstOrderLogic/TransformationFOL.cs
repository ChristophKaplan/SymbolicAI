using System;
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

    private delegate void TransformAction<T>(ref T sentence);

    private static void BottomUpTransformation(ref ISentence sentence, TransformAction<ISentence> transformAction) {
        for (var i = 0; i < sentence.Children.Count; i++) {
            var childSentence = sentence.Children[i];
            BottomUpTransformation(ref childSentence, transformAction);
        }

        transformAction(ref sentence);
    }

    private static void TopDownTransformation(ref ISentence sentence, TransformAction<ISentence> transformAction) {
        transformAction(ref sentence);

        for (var i = 0; i < sentence.Children.Count; i++) {
            var childSentence = sentence.Children[i];
            TopDownTransformation(ref childSentence, transformAction);
        }
    }

    public static void Transform(EquivType equivType, ref ISentence sentence) {
        switch (equivType) {
            case EquivType.SimplifyConstants:
                BottomUpTransformation(ref sentence, SimplifyConstants);
                break;
            case EquivType.DissolveBiconditional:
                BottomUpTransformation(ref sentence, DissolveBiconditional);
                break;
            case EquivType.DissolveImplication:
                BottomUpTransformation(ref sentence, DissolveImplication);
                break;
            case EquivType.PushNegation:
                BottomUpTransformation(ref sentence, PushNegation);
                break;
            case EquivType.DoubleNegation:
                BottomUpTransformation(ref sentence, DoubleNegation);
                break;
            case EquivType.Absorption:
                BottomUpTransformation(ref sentence, Absorption);
                break;
            case EquivType.AssociationAndIdem:
                BottomUpTransformation(ref sentence, AssociationAndIdem);
                break;
            case EquivType.PullQuantifier:
                BottomUpTransformation(ref sentence, PullQuantifier);
                break;
            case EquivType.RemoveDuplicateQuantifier:
                TopDownTransformation(ref sentence, RemoveDuplicateQuantifier);
                break;
            case EquivType.RemoveQuantifier:
                BottomUpTransformation(ref sentence, RemoveQuantifier);
                break;
            case EquivType.DistributionOfDisjunction:
                BottomUpTransformation(ref sentence, DistributionOfDisjunction);
                break;
            case EquivType.DistributionOfConjunction:
                BottomUpTransformation(ref sentence, DistributionOfConjunction);
                break;
        }
    }

    private static void PullQuantifier(ref ISentence sentence) {
        // Trigger on the binary connective, not the quantified child: replacing a node requires
        // writing through `ref sentence`.
        if (sentence is not IComplexSentence { IsBinary: true } connective) {
            return;
        }

        foreach (var child in connective.Children) {
            if (child is not IComplexSentence { IsQuantifier: true } quantified) {
                continue;
            }

            var quantifier = (Quantifier)quantified.Connective;
            var sibling = connective.GetSiblingOf(quantified);

            // Capture avoidance: pulling widens the quantifier's scope over the sibling, so a free
            // occurrence of the bound name there must be renamed away first (an existential would
            // skolemize it into a single witness). Only free occurrences trigger the rename, which
            // keeps same-named universals mergeable; the process-wide counter plus unparseable '$'
            // makes fresh names globally unique.
            if (HasFreeOccurrence(sibling, quantifier.Variable)) {
                var fresh = new Variable($"q${Interlocked.Increment(ref _captureRenameCounter)}");
                SubstituteFree(quantified.Children[0], quantifier.Variable, fresh);
                quantifier = new Quantifier(quantifier.Symbol, fresh);
            }

            var body = new ComplexSentence(quantified.Children[0], connective.Connective.Symbol, sibling);
            var result = new ComplexSentence(quantifier.Clone(), body);
            result.SetParentToParentOf(sentence);
            sentence = result;
            return;
        }
    }

    private static int _captureRenameCounter;

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
    private static void SubstituteFree(ISentence sentence, Variable variable, Variable replacement) {
        if (sentence is IComplexSentence { IsQuantifier: true } quantified &&
            ((Quantifier)quantified.Connective).Variable.Equals(variable)) {
            return;
        }

        if (sentence is IAtomicSentence) {
            sentence.SubstituteTerm(variable, replacement);
            return;
        }

        foreach (var child in sentence.Children) {
            SubstituteFree(child, variable, replacement);
        }
    }

    private static void RemoveDuplicateQuantifier(ref ISentence sentence) {
        if (sentence is not IComplexSentence { IsQuantifier: true } quantifiedSentence) {
            return;
        }

        if (quantifiedSentence.Children[0] is not IComplexSentence { IsQuantifier: true } childQuantified) {
            return;
        }

        if (!Equals(quantifiedSentence.Connective, childQuantified.Connective)) {
            return;
        }

        childQuantified.SetParentToParentOf(sentence);
        sentence = childQuantified;
    }

    private static void SimplifyConstants(ref ISentence sentence) {
        if (sentence is not IComplexSentence complexSentence) {
            return;
        }

        if (complexSentence.IsLiteral) {
            if (complexSentence.Children[0] is IAtomicSentence { IsNullaryConstant: true } constant) {
                constant.Negate(); //push negation
                constant.SetParentToParentOf(sentence);
                sentence = constant;
            }

            return;
        }

        if (complexSentence.Connective.Symbol != Connective.LogicSymbol.CONJUNCTION &&
            complexSentence.Connective.Symbol != Connective.LogicSymbol.DISJUNCTION) {
            return;
        }

        foreach (var child in sentence.Children) {
            if (child is not IAtomicSentence { IsNullaryConstant: true } atomicSentence) {
                continue;
            }

            switch (complexSentence.Connective.Symbol) {
                case Connective.LogicSymbol.CONJUNCTION when atomicSentence.Tautology:
                case Connective.LogicSymbol.DISJUNCTION when atomicSentence.Contradiction:
                    var otherSide = complexSentence.GetSiblingOf(atomicSentence);
                    otherSide.SetParentToParentOf(sentence);
                    sentence = otherSide;
                    break;
                case Connective.LogicSymbol.CONJUNCTION when atomicSentence.Contradiction:
                case Connective.LogicSymbol.DISJUNCTION when atomicSentence.Tautology:
                    atomicSentence.SetParentToParentOf(sentence);
                    sentence = atomicSentence;
                    break;
                default:
                    throw new Exception($"{complexSentence}, {complexSentence.Connective}");
            }
        }
    }

    private static void DissolveBiconditional(ref ISentence sentence) {
        if (sentence is IComplexSentence complexSentence && complexSentence.Connective == Connective.LogicSymbol.BICONDITIONAL) {
            var lhs = complexSentence.Children[0];
            var rhs = complexSentence.Children[1];
            var lhsImplication = new ComplexSentence(lhs, Connective.LogicSymbol.IMPLICATION, rhs);
            var rhsImplication = new ComplexSentence(rhs, Connective.LogicSymbol.IMPLICATION, lhs);
            var and = new ComplexSentence(lhsImplication, Connective.LogicSymbol.CONJUNCTION, rhsImplication);
            and.SetParentToParentOf(sentence);
            sentence = and;
        }
    }

    private static void DissolveImplication(ref ISentence sentence) {
        if (sentence is IComplexSentence complexSentence && complexSentence.Connective == Connective.LogicSymbol.IMPLICATION) {
            var lhs = complexSentence.Children[0];
            var rhs = complexSentence.Children[1];
            var notLhs = new ComplexSentence(Connective.LogicSymbol.NEGATION, lhs);
            var or = new ComplexSentence(notLhs, Connective.LogicSymbol.DISJUNCTION, rhs);
            or.SetParentToParentOf(sentence);
            sentence = or;
        }
    }

    private static void PushNegation(ref ISentence sentence) {
        if (sentence is not IComplexSentence { IsNegation: true } negatedSentence) {
            return;
        }

        //negate quantifier
        if (negatedSentence.Children[0] is IComplexSentence { IsNegation: false, IsQuantifier: true } quantified) {
            quantified.FlipOperator();
            quantified.Children[0].Negate();
            quantified.SetParentToParentOf(sentence);
            sentence = quantified;
        }

        //deMorgan
        else if (negatedSentence.Children[0] is IComplexSentence { IsNegation: false, IsBinary: true } inner) {
            inner.FlipOperator();
            inner.Children[0].Negate();
            inner.Children[1].Negate();
            inner.SetParentToParentOf(sentence);
            sentence = inner;
        }
    }

    private static void DoubleNegation(ref ISentence sentence) {
        if (sentence is IComplexSentence { IsNegation: true } negation &&
            negation.Children[0] is IComplexSentence { IsNegation: true } doubleNegation) {
            var inner = doubleNegation.Children[0];
            inner.SetParentToParentOf(sentence);
            sentence = inner;
        }
    }

    private static void Absorption(ref ISentence sentence) {
        if (!sentence.IsBinary) return;

        var complex = (IComplexSentence)sentence;
        var lhs = sentence.Children[0];
        var rhs = sentence.Children[1];

        if (rhs is IComplexSentence rhsComplex && IsDualOperator(complex.Connective, rhsComplex.Connective) && rhsComplex.Children.Contains(lhs)) {
            lhs.SetParentToParentOf(sentence);
            sentence = lhs;
        }

        if (lhs is IComplexSentence lhsComplex && IsDualOperator(complex.Connective, lhsComplex.Connective) && lhsComplex.Children.Contains(rhs)) {
            rhs.SetParentToParentOf(sentence);
            sentence = rhs;
        }

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

    private static void AssociationAndIdem(ref ISentence sentence) {
        //A AND A)
        //A OR A)
        //(A AND (B AND A)) = (B AND A)
        //(A OR (B OR A)) = (B OR A)

        if (!sentence.IsBinary) return;

        var complex = (IComplexSentence)sentence;
        var lhs = sentence.Children[0];
        var rhs = sentence.Children[1];

        // Plain idempotency: A ∧ A ≡ A and A ∨ A ≡ A.
        if ((complex.IsConjunction || complex.IsDisjunction) && lhs.Equals(rhs)) {
            lhs.SetParentToParentOf(sentence);
            sentence = lhs;
            return;
        }

        if (rhs is IComplexSentence rhsComplex && IsEquivOperator(complex.Connective, rhsComplex.Connective) && rhsComplex.Children.Contains(lhs)) {
            rhs.SetParentToParentOf(sentence);
            sentence = rhs;
        }

        if (lhs is IComplexSentence lhsComplex && IsEquivOperator(complex.Connective, lhsComplex.Connective) && lhsComplex.Children.Contains(rhs)) {
            lhs.SetParentToParentOf(sentence);
            sentence = lhs;
        }

        return;

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

    private static void RemoveQuantifier(ref ISentence sentence) {
        if (sentence is not IComplexSentence { IsQuantifier: true } quantifiedSentence) {
            return;
        }

        var child = quantifiedSentence.Children[0];
        child.SetParentToParentOf(quantifiedSentence);
        sentence = child;
    }

    private static void DistributionOfDisjunction(ref ISentence sentence) {
        //A OR (B AND C) = (A OR B) AND (A OR C)
        Distribute(ref sentence, Connective.LogicSymbol.DISJUNCTION, Connective.LogicSymbol.CONJUNCTION);
    }

    private static void DistributionOfConjunction(ref ISentence sentence) {
        //A AND (B OR C) = (A AND B) OR (A AND C)
        Distribute(ref sentence, Connective.LogicSymbol.CONJUNCTION, Connective.LogicSymbol.DISJUNCTION);
    }

    private static void Distribute(ref ISentence sentence, Connective.LogicSymbol outer, Connective.LogicSymbol inner) {
        if (!sentence.IsBinary) return;
        var complex = (IComplexSentence)sentence;
        if (complex.Connective.Symbol != outer) return;

        var lhs = sentence.Children[0];
        var rhs = sentence.Children[1];

        if (rhs is IComplexSentence rhsComplex && rhsComplex.Connective.Symbol == inner) {
            var newLhs = new ComplexSentence(lhs, outer, rhs.Children[0]);
            var newRhs = new ComplexSentence(lhs, outer, rhs.Children[1]);
            var result = new ComplexSentence(newLhs, inner, newRhs);
            result.SetParentToParentOf(sentence);
            sentence = result;
        }
        else if (lhs is IComplexSentence lhsComplex && lhsComplex.Connective.Symbol == inner) {
            var newLhs = new ComplexSentence(lhs.Children[0], outer, rhs);
            var newRhs = new ComplexSentence(lhs.Children[1], outer, rhs);
            var result = new ComplexSentence(newLhs, inner, newRhs);
            result.SetParentToParentOf(sentence);
            sentence = result;
        }
    }
}