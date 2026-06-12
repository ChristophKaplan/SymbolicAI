using System.Collections.Generic;

namespace FirstOrderLogic
{
    // A belief base that can answer questions about itself and other theories.
    public interface ITheory
    {
        List<ISentence> State { get; }

        // Does this theory entail `target`? (Resolution.)
        bool Entails(ISentence target);

        // Minimal premise sets of this theory that prove `target`. (Kernels.)
        List<List<ISentence>> Explain(ISentence target);

        // Bucket each of this theory's sentences against `other`: agreement / contradiction /
        // silence. Directional (this → other).
        TheoryComparison Compare(ITheory? other, ComparisonMode mode = ComparisonMode.Syntactic);

        // Symmetric: the two theories are jointly consistent.
        bool IsConsistentWith(ITheory? other, ComparisonMode mode = ComparisonMode.Syntactic);

        // The complementary literal pairs in this theory's own deductive closure.
        List<TheoryConflict> Conflicts();

        bool IsConsistent();
    }
}
