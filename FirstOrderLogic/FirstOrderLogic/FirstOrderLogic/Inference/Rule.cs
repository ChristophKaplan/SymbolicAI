using System;
using System.Collections.Generic;
using System.Linq;

namespace FirstOrderLogic
{
    // l1 ∧ … ∧ ln ⇒ h over literals of either polarity; a fact is the n = 0 case. Negation is
    // explicit falsehood (¬P matches only ¬P), not negation-as-failure. Variables are implicitly
    // universally quantified.
    public class Rule
    {
        public IReadOnlyList<ISentence> Premises { get; }
        public ISentence Head { get; }
        public bool IsFact => Premises.Count == 0;

        public Rule(ISentence head, IReadOnlyList<ISentence> premises)
        {
            Head = head;
            Premises = premises;
        }

        // Null if `sentence` is not a rule (bare literal, or literal-conjunction ⇒ literal; a
        // leading universal-quantifier prefix is tolerated). Works on a clone.
        public static Rule? From(ISentence sentence)
        {
            var s = StripUniversals(sentence.Clone());

            if (s.IsLiteral)
                return new Rule(s, Array.Empty<ISentence>());

            if (!s.IsImplication) return null;

            var head = s.Children[1];
            if (!head.IsLiteral) return null;

            var premises = new List<ISentence>();
            return CollectConjuncts(s.Children[0], premises)
                ? new Rule(head, premises)
                : null;
        }

        // Standardize apart: fresh variable names so repeated uses of the same rule cannot capture
        // each other's bindings.
        public Rule Renamed(int id)
        {
            var rename = new Dictionary<Variable, Term>();
            foreach (var v in Variables()) rename[v] = new Variable(v.TermSymbol + "#" + id);
            var head = Bindings.Apply(Head, rename);
            var premises = Premises.Select(p => Bindings.Apply(p, rename)).ToList();
            return new Rule(head, premises);
        }

        private IEnumerable<Variable> Variables()
        {
            var all = Bindings.VariablesOf(Head).ToList();
            foreach (var p in Premises) all.AddRange(Bindings.VariablesOf(p));
            return all.Distinct();
        }

        private static ISentence StripUniversals(ISentence s)
        {
            while (s is IComplexSentence { IsQuantifier: true } q &&
                   q.Connective == Connective.LogicSymbol.UNIVERSAL)
                s = q.Children[0];
            return s;
        }

        private static bool CollectConjuncts(ISentence s, List<ISentence> acc)
        {
            if (s is IComplexSentence c && c.IsConjunction)
                return CollectConjuncts(c.Children[0], acc) && CollectConjuncts(c.Children[1], acc);
            if (!s.IsLiteral) return false;
            acc.Add(s);
            return true;
        }

        public override string ToString() =>
            IsFact ? Head.ToString() : string.Join(" ∧ ", Premises) + " ⇒ " + Head;
    }
}
