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
            Connective = RequireQuantifierCarriesVariable(connective);
            Children = new[] { p };
        }

        // Same invariant as RequireNonQuantifier, from the other direction: a quantifier symbol
        // is only usable when the connective is a Quantifier carrying the bound variable.
        private static Connective RequireQuantifierCarriesVariable(Connective connective) {
            if (connective is not Quantifier &&
                (connective.Symbol == Connective.LogicSymbol.UNIVERSAL ||
                 connective.Symbol == Connective.LogicSymbol.EXISTENTIAL)) {
                throw new ArgumentException(
                    "Quantifier nodes need a Quantifier connective carrying the bound variable — use ComplexSentence(Quantifier, ISentence).",
                    nameof(connective));
            }

            return connective;
        }

        public ISentence GetSiblingOf(ISentence sentence) {
            if (Children.Count != 2) {
                throw new InvalidOperationException("Error: ComplexSentence must have two children.");
            }

            if (Children[0].Equals(sentence)) {
                return Children[1];
            }

            if (Children[1].Equals(sentence)) {
                return Children[0];
            }

            throw new ArgumentException("Error: Sentence not found in ComplexSentence.", nameof(sentence));
        }

        // Capture-avoiding: a target mentioning the bound variable — the variable itself, or a
        // compound like f(x) — has no free occurrence in this scope, so substituting it stops
        // here; a replacement that mentions the bound name renames the binder first.
        public override ISentence Substitute(Term target, Term replacement) {
            if (IsQuantifier) {
                var quantifier = (Quantifier)Connective;
                if (target.Occurs(quantifier.Variable)) {
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

        protected override int ComputeHashCode() {
            return HashCode.Combine(Connective.GetHashCode(), base.ComputeHashCode());
        }
    
        protected override bool EqualsCore(Sentence other) {
            return Connective.Equals(((ComplexSentence)other).Connective) && base.EqualsCore(other);
        }

        public override string ToString() {
            return Children.Count == 1 ? $"{Connective} {Children[0]}" : $"({Children[0]} {Connective} {Children[1]})";
        }
    }
}