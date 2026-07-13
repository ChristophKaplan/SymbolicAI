using System.Collections.Generic;

namespace FirstOrderLogic
{
    public readonly struct Stance
    {
        public IReadOnlyList<ISentence> Agreements    { get; }  // other holds s
        public IReadOnlyList<ISentence> Disagreements { get; }  // other holds ¬s
        public IReadOnlyList<ISentence> Silences      { get; }  // other holds neither

        public Stance(IReadOnlyList<ISentence> agreements,
                      IReadOnlyList<ISentence> disagreements,
                      IReadOnlyList<ISentence> silences)
        {
            Agreements    = agreements;
            Disagreements = disagreements;
            Silences      = silences;
        }
        
        public float AgreementShare    => Verdicts == 0 ? 0f : (float)Agreements.Count    / Verdicts;
        public float DisagreementShare => Verdicts == 0 ? 0f : (float)Disagreements.Count / Verdicts;

        private int Verdicts => Agreements.Count + Disagreements.Count;
    }
}
