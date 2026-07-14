using System;
using System.Collections.Generic;

namespace FirstOrderLogic {
    public interface IAtomicSentence : ISentence
    {
        string Symbol { get; }
        bool IsNullaryConstant { get; }
        bool Tautology { get; }
        bool Contradiction { get; }
    }

    public abstract class AtomicSentence : Sentence, IAtomicSentence
    {
        public string Symbol { get; }
        public bool IsNullaryConstant => Tautology || Contradiction;
        public bool Tautology => Symbol.Equals(Connective.SymbolToString(Connective.LogicSymbol.TRUE));
        public bool Contradiction => Symbol.Equals(Connective.SymbolToString(Connective.LogicSymbol.FALSE));

        protected AtomicSentence(string symbol)
        {
            Symbol = symbol;
        }

        public override ISentence Negated()
        {
            if (Tautology) return Constant(Connective.LogicSymbol.FALSE);
            if (Contradiction) return Constant(Connective.LogicSymbol.TRUE);
            return new ComplexSentence(Connective.LogicSymbol.NEGATION, this);
        }

        // Nullary constants (TRUE/FALSE) are always propositions.
        private static ISentence Constant(Connective.LogicSymbol symbol)
        {
            return new Proposition(Connective.SymbolToString(symbol));
        }

        public override ISentence WithChildren(IReadOnlyList<ISentence> children) => this;

        public override bool Equals(object? obj) {
            if (obj == null || GetType() != obj.GetType()) {
                return false;
            }

            var other = (AtomicSentence)obj;
            return Symbol.Equals(other.Symbol);
        }

        public override int GetHashCode() {
            return Symbol.GetHashCode();
        }

        public override string ToString() => Symbol;
    }
}