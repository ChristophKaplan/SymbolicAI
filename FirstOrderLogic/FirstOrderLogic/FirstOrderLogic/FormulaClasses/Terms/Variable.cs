namespace FirstOrderLogic {
    public class Variable : Term {
        public Variable(string termSymbol) : base(termSymbol) {
        }
        public Variable(Variable other) : base(other.TermSymbol) {
        }

        // A variable is only equal to another variable with the same symbol — never to a
        // constant/function that happens to share its name (keeps equality symmetric).
        public override bool Equals(object? obj) => obj is Variable other && TermSymbol == other.TermSymbol;

        public override int GetHashCode() => TermSymbol.GetHashCode();
    }
}