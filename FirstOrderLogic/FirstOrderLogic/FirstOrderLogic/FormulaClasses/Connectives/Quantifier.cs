using System;

namespace FirstOrderLogic {
    public class Quantifier : Connective {
        public readonly Variable Variable;

        public Quantifier(LogicSymbol symbol, Variable variable) : base(symbol) {
            Variable = variable;
        }
    
        public Quantifier(Quantifier quantifier) : base(quantifier.Symbol) {
            Variable = new Variable(quantifier.Variable);
        }
    
        public override Connective Clone() => new Quantifier(this);

        public override string ToString() {
            return $"{base.ToString()} {Variable}";
        }

        public override bool Equals(object? obj) {
            return obj is Quantifier quantifier && Symbol == quantifier.Symbol && Variable.Equals(quantifier.Variable);
        }
    
        public override int GetHashCode() {
            return HashCode.Combine(Symbol, Variable);
        }
    }
}
