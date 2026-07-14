using System.Collections.Generic;
using System.Linq;

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
            foreach (var s in rules)
            {
                if (s is not IComplexSentence rule || !rule.IsImplication)
                {
                    continue;
                }

                bool holds;
                try
                {
                    holds = model.Evaluate(UniversalClosureOf(rule.Children[0]));
                }
                catch (InterpretationException)
                {
                    continue; // model doesn't cover this rule's symbols — skip
                }

                if (holds)
                {
                    result.Add(rule.Children[1]);
                }
            }

            return result;
        }

        // Free variables are implicitly universal library-wide, so the antecedent of
        // Human(x) => Mortal(x) is the claim ∀x Human(x). Without the closure it hits an unbound
        // variable, and the resulting InterpretationException is indistinguishable from the
        // genuine "model doesn't cover this symbol" skip — silently dropping universal rules.
        private static ISentence UniversalClosureOf(ISentence sentence)
        {
            return FreeVariablesOf(sentence, new List<Variable>())
                .Distinct()
                .Aggregate(sentence, (body, variable) =>
                    new ComplexSentence(new Quantifier(Connective.LogicSymbol.UNIVERSAL, variable), body));
        }

        private static IEnumerable<Variable> FreeVariablesOf(ISentence sentence, IReadOnlyCollection<Variable> bound)
        {
            if (sentence is IPredicate predicate)
            {
                return predicate.GetVariables().Where(variable => !bound.Contains(variable));
            }

            if (sentence is IComplexSentence { IsQuantifier: true } quantified)
            {
                var inner = bound.Append(((Quantifier)quantified.Connective).Variable).ToList();
                return FreeVariablesOf(quantified.Children[0], inner);
            }

            return sentence.Children.SelectMany(child => FreeVariablesOf(child, bound));
        }
    }
}
