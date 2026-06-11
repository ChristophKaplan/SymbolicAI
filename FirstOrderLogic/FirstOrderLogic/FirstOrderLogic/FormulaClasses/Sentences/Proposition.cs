namespace FirstOrderLogic {
    public interface IProposition : IAtomicSentence {}

    public class Proposition : AtomicSentence, IProposition {
        public Proposition(string propositionSymbol) : base(propositionSymbol) {}
        public Proposition(string propositionSymbol, int time) : base(propositionSymbol, time) {}
        private Proposition(IProposition other) : base(other) {}
        public override ISentence Clone() => new Proposition(this);
    
        public override void SubstituteTerm(Term term, Term replacement) {
            //no terms
        }
    }
}