namespace FirstOrderLogic
{
    // Inference regime for theory comparison, ordered by deductive strength and cost:
    //   Syntactic — sentence identity, no inference. Cheap, per-tick safe.
    //   Chaining  — membership in the forward-chaining closure of the rule subset. Sound,
    //               incomplete beyond literal rules (no case analysis, no disjunction). Moderate.
    //   Semantic  — Resolution entailment. Sound and refutation-complete, exponential — on-demand.
    public enum ComparisonMode
    {
        Syntactic,
        Chaining,
        Semantic,
    }
}
