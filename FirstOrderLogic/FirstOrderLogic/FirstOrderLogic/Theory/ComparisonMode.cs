namespace FirstOrderLogic
{
    // Syntactic: literal match (cheap, per-tick safe). Semantic: Resolution entailment
    // (catches chained inference, exponential — on-demand only).
    public enum ComparisonMode
    {
        Syntactic,
        Semantic,
    }
}
