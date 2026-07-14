namespace FirstOrderLogic {
    public interface IProposition : IAtomicSentence {}

    public class Proposition : AtomicSentence, IProposition {
        public Proposition(string propositionSymbol) : base(propositionSymbol) {}

        public override ISentence Substitute(Term target, Term replacement) => this;
    }
}