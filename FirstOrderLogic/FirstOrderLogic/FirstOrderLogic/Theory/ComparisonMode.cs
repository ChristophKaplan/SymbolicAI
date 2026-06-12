namespace FirstOrderLogic
{
    // Inference regime for theory comparison, ordered by deductive strength and cost:
    // Syntactic = sentence identity, Chaining = forward-chaining closure membership,
    // Semantic = Resolution entailment.
    public enum ComparisonMode
    {
        Syntactic,
        Chaining,
        Semantic,
    }
}
