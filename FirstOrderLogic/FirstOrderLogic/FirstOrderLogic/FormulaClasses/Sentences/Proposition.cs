namespace FirstOrderLogic {
    public interface IProposition : IAtomicSentence {}

    public class Proposition : AtomicSentence, IProposition {
        public Proposition(string propositionSymbol) : base(propositionSymbol) {}
        public Proposition(string propositionSymbol, int time) : base(propositionSymbol, time) {}

        public override ISentence Substitute(Term target, Term replacement) => this;

        public override ISentence WithTimeShift(int offset) =>
            Time.HasValue ? new Proposition(Symbol, Time.Value + offset) : this;
    }
}