using System;

namespace FirstOrderLogic {
    public interface IAtomicSentence : ISentence
    {
        string Symbol { get; set; }
        public int? Time { get; set; }
        bool IsNullaryConstant { get; }
        bool Tautology { get; }
        bool Contradiction { get; }
    }

    public abstract class AtomicSentence : Sentence, IAtomicSentence
    {
        public string Symbol { get; set; }
        public int? Time { get; set; }
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

        protected AtomicSentence(IAtomicSentence other)
        {
            Parent = null; //other.Parent;
            Symbol = other.Symbol;
            Time = other.Time;
        }

        public override ISentence Negate()
        {
            if (Tautology)
            {
                var clone = (IAtomicSentence)Clone();
                clone.Symbol = Connective.SymbolToString(Connective.LogicSymbol.FALSE);
                return clone;
            }
        
            if (Contradiction)
            {
                var clone = (IAtomicSentence)Clone();
                clone.Symbol = Connective.SymbolToString(Connective.LogicSymbol.TRUE);
                return clone;
            }

            var negated = new ComplexSentence(Connective.LogicSymbol.NEGATION, Clone());
            negated.SetParentToParentOf(this);
            return negated;
        }

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