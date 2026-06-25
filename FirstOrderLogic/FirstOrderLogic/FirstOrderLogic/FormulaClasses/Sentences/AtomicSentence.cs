using System;
using System.Collections.Generic;

namespace FirstOrderLogic {
    public interface IAtomicSentence : ISentence
    {
        string Symbol { get; }
        public int? Time { get; }
        bool IsNullaryConstant { get; }
        bool Tautology { get; }
        bool Contradiction { get; }
    }

    public abstract class AtomicSentence : Sentence, IAtomicSentence
    {
        public string Symbol { get; }
        public int? Time { get; }
        public bool IsNullaryConstant => Tautology || Contradiction;
        public bool Tautology => Symbol.Equals(Connective.SymbolToString(Connective.LogicSymbol.TRUE));
        public bool Contradiction => Symbol.Equals(Connective.SymbolToString(Connective.LogicSymbol.FALSE));

        protected AtomicSentence(string symbol)
        {
            Symbol = symbol;
            Time = null;
        }
    
        protected AtomicSentence(string symbol, int time)
        {
            Symbol = symbol;
            Time = time;
        }

        public override ISentence Negated()
        {
            if (Tautology) return Constant(Connective.LogicSymbol.FALSE);
            if (Contradiction) return Constant(Connective.LogicSymbol.TRUE);
            return new ComplexSentence(Connective.LogicSymbol.NEGATION, this);
        }

        // Nullary constants (TRUE/FALSE) are always propositions.
        private ISentence Constant(Connective.LogicSymbol symbol)
        {
            var name = Connective.SymbolToString(symbol);
            return Time.HasValue ? new Proposition(name, Time.Value) : new Proposition(name);
        }

        public override ISentence WithChildren(IReadOnlyList<ISentence> children) => this;

        public override bool Equals(object? obj) {
            if (obj == null || GetType() != obj.GetType()) {
                return false;
            }

            var other = (AtomicSentence)obj;
            return Symbol.Equals(other.Symbol) && Time == other.Time;
        }
    
        public override int GetHashCode() {
            return HashCode.Combine(Symbol, Time);
        }

        public override string ToString() => $"{Symbol}{(Time.HasValue ? $"^{Time}" : "")}";
    }
}