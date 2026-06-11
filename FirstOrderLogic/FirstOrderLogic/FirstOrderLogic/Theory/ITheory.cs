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

        // Bucket each of this theory's sentences against `other`: agreement / contradiction / silence,
        // plus the alignment ratio. Directional (this → other).
        TheoryComparison Compare(ITheory? other, ComparisonMode mode = ComparisonMode.Syntactic);

        // Symmetric: the two theories are jointly consistent. Chaining: the union's closure holds
        // no complementary literal pair; Semantic: the union is satisfiable; Syntactic: no literal
        // clash in either Compare direction.
        bool IsConsistentWith(ITheory? other, ComparisonMode mode = ComparisonMode.Syntactic);

        // The complementary literal pairs in this theory's own deductive closure.
        List<TheoryConflict> Conflicts();

        bool IsConsistent();
    }
}
