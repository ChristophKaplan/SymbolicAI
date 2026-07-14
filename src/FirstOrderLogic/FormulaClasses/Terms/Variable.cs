namespace FirstOrderLogic {
    public class Variable : Term {
        public Variable(string termSymbol) : base(termSymbol) {
        }
        public Variable(Variable other) : base(other.TermSymbol) {
        }

        protected override bool EqualsCore(Term other) => other is Variable variable && TermSymbol == variable.TermSymbol;

        protected override int ComputeHashCode() => TermSymbol.GetHashCode();
    }
}