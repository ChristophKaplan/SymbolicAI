using System.Collections.Generic;

namespace FirstOrderLogic
{
    // Model-theoretic detachment: the consequents whose rule antecedents hold in a given
    // interpretation. The semantic dual of ForwardChaining — truth comes from the model,
    // not from unifying against asserted facts. One step; no head instantiation, no fixpoint.
    public static class SemanticChaining
    {
        public static List<ISentence> Detach(IEnumerable<ISentence> rules, Interpretation model)
        {
            var result = new List<ISentence>();
            if (model == null) return result;
            foreach (var s in rules)
            {
                if (s is not IComplexSentence rule || !rule.IsImplication) continue;
                bool holds;
                try   { holds = model.Evaluate(rule.Children[0]); }
                catch { continue; } // model can't evaluate this rule (unknown predicate/term) — skip
                if (holds) result.Add(rule.Children[1]);
            }
            return result;
        }
    }
}
