using System.Collections.Generic;

namespace FirstOrderLogic
{
    public interface ITheory
    {
        List<ISentence> State { get; }
        
        bool Entails(ISentence target);
        
        List<List<ISentence>> Explain(ISentence target);

        TheoryComparison Compare(ITheory? other, ComparisonMode mode = ComparisonMode.Chaining);

        bool IsConsistentWith(ITheory? other, ComparisonMode mode = ComparisonMode.Chaining);
        
        List<(ISentence Claim, ISentence Counter)> Conflicts();

        bool IsConsistent();
    }
}
