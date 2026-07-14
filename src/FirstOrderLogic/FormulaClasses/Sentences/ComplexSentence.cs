using System;
using System.Collections.Generic;
using System.Threading;

namespace FirstOrderLogic {
    public interface IComplexSentence : ISentence{
        Connective Connective { get; }
        bool IsQuantifier { get; }
        bool IsConjunction { get; }
        bool IsDisjunction { get; }
        ISentence GetSiblingOf(ISentence sentence);
    }

    public class ComplexSentence : Sentence, IComplexSentence{
        public Connective Connective { get; }
        public bool IsQuantifier => Connective == Connective.LogicSymbol.EXISTENTIAL || Connective == Connective.LogicSymbol.UNIVERSAL;
        public bool IsConjunction => Connective == Connective.LogicSymbol.CONJUNCTION;
        public bool IsDisjunction => Connective == Connective.LogicSymbol.DISJUNCTION;
        public ComplexSentence(ISentence p, Connective.LogicSymbol logicSymbol, ISentence q) {
            Connective = new Connective(RequireNonQuantifier(logicSymbol));
            Children = new[] { p, q };
        }

        public ComplexSentence(Connective.LogicSymbol logicSymbol, ISentence p) {
            Connective = new Connective(RequireNonQuantifier(logicSymbol));
            Children = new[] { p };
        }

        // A quantifier node built from the bare symbol would carry a plain Connective, and every
        // consumer downcasts IsQuantifier nodes to Quantifier for the bound variable.
        private static Connective.LogicSymbol RequireNonQuantifier(Connective.LogicSymbol logicSymbol) {
            if (logicSymbol == Connective.LogicSymbol.UNIVERSAL || logicSymbol == Connective.LogicSymbol.EXISTENTIAL) {
                throw new ArgumentException(
                    "Quantifier nodes need a Quantifier connective carrying the bound variable — use ComplexSentence(Quantifier, ISentence).",
                    nameof(logicSymbol));
            }

            return logicSymbol;
        }

        public ComplexSentence(Connective connective, ISentence p) {
            Connective = connective;
            Children = new[] { p };
        }

        public ISentence GetSiblingOf(ISentence sentence) {
            if (Children.Count != 2) {
                throw new Exception("Error: ComplexSentence must have two children.");
            }

            if (Children[0].Equals(sentence)) {
                return Children[1];
            }

            if (Children[1].Equals(sentence)) {
                return Children[0];
            }

            throw new Exception("Error: Sentence not found in ComplexSentence.");
        }

        // Capture-avoiding: occurrences of the bound variable are not free, so substituting it
        // stops here, and a replacement that mentions the bound name renames the binder first.
        public override ISentence Substitute(Term target, Term replacement) {
            if (IsQuantifier) {
                var quantifier = (Quantifier)Connective;
                if (quantifier.Variable.Equals(target)) {
                    return this;
                }

                if (replacement.Occurs(quantifier.Variable)) {
                    var fresh = new Variable($"r${Interlocked.Increment(ref _captureRenameCounter)}");
                    var body = Children[0].Substitute(quantifier.Variable, fresh);
                    return new ComplexSentence(new Quantifier(quantifier.Symbol, fresh), body)
                        .Substitute(target, replacement);
                }
            }

            if (IsBinary) {
                return new ComplexSentence(
                    Children[0].Substitute(target, replacement),
                    Connective.Symbol,
                    Children[1].Substitute(target, replacement));
            }

            return new ComplexSentence(Connective.Clone(), Children[0].Substitute(target, replacement));
        }

        // '$' is unparseable, so a rename can never collide with a user symbol.
        private static int _captureRenameCounter;
    
        public override ISentence Negated() =>
            IsNegation ? Children[0] : new ComplexSentence(Connective.LogicSymbol.NEGATION, this);

        public override ISentence WithChildren(IReadOnlyList<ISentence> children) =>
            children.Count == 1
                ? new ComplexSentence(Connective.Clone(), children[0])
                : new ComplexSentence(children[0], Connective.Symbol, children[1]);

        public override int GetHashCode() {
            return HashCode.Combine(Connective.GetHashCode(), base.GetHashCode());
        }
    
        public override bool Equals(object? obj) {
            if (obj == null || GetType() != obj.GetType()) {
                return false;
            }

            var other = (ComplexSentence)obj;
            return Connective.Equals(other.Connective) && base.Equals(other);
        }

        public override string ToString() {
            return Children.Count == 1 ? $"{Connective} {Children[0]}" : $"({Children[0]} {Connective} {Children[1]})";
        }
    }
}