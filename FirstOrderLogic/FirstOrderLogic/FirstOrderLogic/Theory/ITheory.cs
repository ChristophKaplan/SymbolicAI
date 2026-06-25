using System.Collections.Generic;

namespace FirstOrderLogic
{
    public interface ITheory
    {
        IReadOnlyList<ISentence> State { get; }

        bool Entails(ISentence target);
        
        List<List<ISentence>> Explain(ISentence target);

        List<ISentence> Agreements(ITheory? other, ComparisonMode mode = ComparisonMode.Chaining);

        List<ISentence> Conflicts(ITheory? other, ComparisonMode mode = ComparisonMode.Chaining);

        List<ISentence> Silences(ITheory? other, ComparisonMode mode = ComparisonMode.Chaining);

        bool IsConsistentWith(ITheory? other, ComparisonMode mode = ComparisonMode.Chaining);

        List<ISentence> Inconsistencies();

        bool IsConsistent(ComparisonMode mode = ComparisonMode.Chaining);
    }
}
