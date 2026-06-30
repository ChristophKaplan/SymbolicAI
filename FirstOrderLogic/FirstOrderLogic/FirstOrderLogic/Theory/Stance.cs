using System.Collections.Generic;

namespace FirstOrderLogic
{
    // How one theory stands relative to another: a partition of our sentences by the other's verdict.
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
        
        // Shares are over verdicts (agreements + disagreements), not the total — silence carries no weight,
        // so the two shares sum to 1 when there is any verdict and are both 0 when the other only stays silent.
        public float AgreementShare    => Verdicts == 0 ? 0f : (float)Agreements.Count    / Verdicts;
        public float DisagreementShare => Verdicts == 0 ? 0f : (float)Disagreements.Count / Verdicts;

        private int Verdicts => Agreements.Count + Disagreements.Count;
    }
}
