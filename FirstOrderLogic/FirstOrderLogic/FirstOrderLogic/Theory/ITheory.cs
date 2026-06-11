using System.Collections.Generic;

namespace FirstOrderLogic
{
    // A set of sentences that can answer questions about itself and other theories.
    public interface ITheory
    {
        List<ISentence> State { get; }

        // Does this theory prove `target`? (Resolution.)
        bool Entails(ISentence target);

        // Minimal premise sets of this theory that prove `target`. (Kernels.)
        List<List<ISentence>> Explain(ISentence target);

        // Bucket each of this theory's sentences against `other`: agreement / contradiction / silence,
        // plus the alignment ratio. Directional (this → other).
        TheoryComparison Compare(ITheory? other, ComparisonMode mode = ComparisonMode.Syntactic);

        // Symmetric: neither theory contradicts the other.
        bool IsConsistentWith(ITheory? other, ComparisonMode mode = ComparisonMode.Syntactic);
    }
}
