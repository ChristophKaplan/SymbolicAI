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

        // 1 = every verdict agrees, 0 = every verdict disagrees, 0.5 = no verdict (silence only / empty).
        public float Alignment
        {
            get
            {
                var decided = Agreements.Count + Disagreements.Count;
                return decided == 0 ? 0.5f : (float)Agreements.Count / decided;
            }
        }
    }
}
