using System.Collections.Generic;

namespace FirstOrderLogic
{
    // A claim and its complement, each held or derived somewhere. Detection only — use KernelSets
    // to explain why either side holds.
    public readonly struct TheoryConflict
    {
        public readonly ISentence Claim;
        public readonly ISentence Counter;

        public TheoryConflict(ISentence claim, ISentence counter)
        {
            Claim   = claim;
            Counter = counter;
        }

        // Every complementary literal pair among `literals`, the positive side as Claim.
        public static List<TheoryConflict> FindAll(IReadOnlyList<ISentence> literals)
        {
            var pairs = new List<TheoryConflict>();
            for (var i = 0; i < literals.Count; i++)
                for (var j = i + 1; j < literals.Count; j++)
                    if (literals[i].IsNegationOf(literals[j]))
                        pairs.Add(literals[i].IsNegation
                            ? new TheoryConflict(literals[j], literals[i])
                            : new TheoryConflict(literals[i], literals[j]));
            return pairs;
        }

        public override string ToString() => $"{Claim} ⊥ {Counter}";
    }
}
