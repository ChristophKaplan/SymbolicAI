using System.Collections.Generic;

namespace FirstOrderLogic
{
    public interface ITheory
    {
        IReadOnlyList<ISentence> State { get; }

        bool Entails(ISentence target);
        
        List<List<ISentence>> Explain(ISentence target);

        Stance Compare(ITheory? other, ComparisonMode mode = ComparisonMode.Syntactic);

        bool IsConsistentWith(ITheory? other, ComparisonMode mode = ComparisonMode.Syntactic);

        List<ISentence> Inconsistencies();

        List<ISentence> Inconsistencies(ITheory? other, ComparisonMode mode = ComparisonMode.Syntactic);

        bool IsConsistent();
    }
}
