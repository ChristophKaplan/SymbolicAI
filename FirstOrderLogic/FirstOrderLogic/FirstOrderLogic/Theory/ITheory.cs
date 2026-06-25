using System.Collections.Generic;

namespace FirstOrderLogic
{
    public interface ITheory
    {
        IReadOnlyList<ISentence> State { get; }

        bool Entails(ISentence target);
        
        List<List<ISentence>> Explain(ISentence target);

        List<ISentence> Agreements(ITheory? other, ComparisonMode mode = ComparisonMode.Syntactic);

        List<ISentence> Conflicts(ITheory? other, ComparisonMode mode = ComparisonMode.Syntactic);

        List<ISentence> Silences(ITheory? other, ComparisonMode mode = ComparisonMode.Syntactic);

        bool IsConsistentWith(ITheory? other, ComparisonMode mode = ComparisonMode.Syntactic);

        List<ISentence> Inconsistencies();

        List<ISentence> Inconsistencies(ITheory? other, ComparisonMode mode = ComparisonMode.Syntactic);

        bool IsConsistent();
    }
}
