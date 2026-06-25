using System.Collections.Generic;

namespace FirstOrderLogic
{
    public readonly struct TheoryComparison
    {
        public readonly List<ISentence> Agreements;
        public readonly List<(ISentence Claim, ISentence Counter)> Contradictions;
        public readonly List<ISentence> Silences;
        public readonly ComparisonMode Mode;

        public TheoryComparison(List<ISentence> agreements, List<(ISentence Claim, ISentence Counter)> contradictions, List<ISentence> silences, ComparisonMode mode)
        {
            Agreements     = agreements;
            Contradictions = contradictions;
            Silences       = silences;
            Mode           = mode;
        }

        // 0 = every verdict was a contradiction, 1 = every verdict was agreement,
        // 0.5 = no verdict (silence only / empty). Silences never count.
        public float Alignment
        {
            get
            {
                var total = Agreements.Count + Contradictions.Count;
                return total == 0 ? 0.5f : (float)Agreements.Count / total;
            }
        }

        public bool HasContradiction => Contradictions.Count > 0;
        public bool IsConsistent     => Contradictions.Count == 0;
    }
}
