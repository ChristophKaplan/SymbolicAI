namespace FirstOrderLogic {
    public class Variable : Term {
        public Variable(string termSymbol) : base(termSymbol) {
        }
        public Variable(Variable other) : base(other.TermSymbol) {
        }

        public override bool Equals(object? obj) => obj is Variable other && TermSymbol == other.TermSymbol;

        public override int GetHashCode() => TermSymbol.GetHashCode();
    }
}