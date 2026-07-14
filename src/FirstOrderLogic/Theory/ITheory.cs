using System.Collections.Generic;

namespace FirstOrderLogic
{
    public interface ITheory : IReadOnlyList<ISentence>
    {

        // maxRounds (0 = unlimited) bounds the underlying resolution saturation; when exceeded,
        // Resolution throws instead of looping forever on a semi-decidable question.
        bool Entails(ISentence target, int maxRounds = 0);

        List<List<ISentence>> Explain(ISentence target, int maxRounds = KernelSets.DefaultMaxRounds);

        Stance Compare(ITheory? other, ComparisonMode mode = ComparisonMode.Syntactic, int maxRounds = 0);

        bool IsConsistentWith(ITheory? other, ComparisonMode mode = ComparisonMode.Syntactic, int maxRounds = 0);

        List<ISentence> Inconsistencies();

        List<ISentence> Inconsistencies(ITheory? other, ComparisonMode mode = ComparisonMode.Syntactic);

        bool IsConsistent();
    }
}
