using System;
using System.Collections.Generic;
using System.Linq;

namespace FirstOrderLogic
{
    // l1 ∧ … ∧ ln ⇒ h over literals of either polarity; a fact is the n = 0 case. Negation is
    // explicit falsehood (¬P matches only ¬P), not negation-as-failure. Variables are implicitly
    // universally quantified.
    //
    // NAF premises are kept separately: `NAF l` holds when l is NOT derivable (closed-world
    // negation). They never bind variables — failure produces no substitution — so a variable
    // occurring only under NAF reads "no derivable instance" (∄). Consumed by the stratified
    // ForwardChaining.Saturate.
    public class Rule
    {
        public IReadOnlyList<ISentence> Premises { get; }
        public IReadOnlyList<ISentence> NafPremises { get; }
        public ISentence Head { get; }
        public bool IsFact => Premises.Count == 0 && NafPremises.Count == 0;

        public Rule(ISentence head, IReadOnlyList<ISentence> premises)
            : this(head, premises, Array.Empty<ISentence>())
        {
        }

        public Rule(ISentence head, IReadOnlyList<ISentence> premises, IReadOnlyList<ISentence> nafPremises)
        {
            Head = head;
            Premises = premises;
            NafPremises = nafPremises;
        }

        // The rule subset of `kb`; non-rule sentences are dropped.
        public static List<Rule> FromAll(IEnumerable<ISentence> kb) => kb.Select(From).OfType<Rule>().ToList();

        // Whether forward chaining can consume this sentence (a fact or a chainable rule) — the
        // queryable form of the fragment decision From makes. Unsafe rules still throw.
        public static bool IsChainable(ISentence sentence) => From(sentence) != null;

        // Null if `sentence` is not a rule (bare literal, or literal-conjunction ⇒ literal, where
        // conjuncts may be NAF-wrapped literals; a leading universal-quantifier prefix is
        // tolerated). Works on a clone.
        public static Rule? From(ISentence sentence)
        {
            var s = StripUniversals(sentence);

            if (s.IsLiteral)
                return new Rule(s, Array.Empty<ISentence>());

            if (!s.IsImplication) return null;

            var head = s.Children[1];
            if (!head.IsLiteral) return null;

            var premises = new List<ISentence>();
            var nafPremises = new List<ISentence>();
            if (!CollectConjuncts(s.Children[0], premises, nafPremises)) return null;

            var bound = new HashSet<Variable>(premises.SelectMany(p => p.VariablesOf()));
            var unbound = head.VariablesOf().Where(v => !bound.Contains(v)).Distinct().ToList();
            if (unbound.Count > 0)
                throw new ArgumentException(
                    $"Unsafe rule '{new Rule(head, premises, nafPremises)}': head variable(s) " +
                    $"{string.Join(", ", unbound.Select(v => v.TermSymbol))} not bound by the body.");

            return new Rule(head, premises, nafPremises);
        }

        // Standardize apart: fresh variable names so repeated uses of the same rule cannot capture
        // each other's bindings.
        public Rule Renamed(int id)
        {
            var rename = new Dictionary<Variable, Term>();
            foreach (var v in Variables()) rename[v] = new Variable(v.TermSymbol + "#" + id);
            var substitution = new Substitution(rename);
            var head = substitution.Apply(Head);
            var premises = Premises.Select(substitution.Apply).ToList();
            var nafPremises = NafPremises.Select(substitution.Apply).ToList();
            return new Rule(head, premises, nafPremises);
        }

        private IEnumerable<Variable> Variables()
        {
            var all = Head.VariablesOf().ToList();
            foreach (var p in Premises) all.AddRange(p.VariablesOf());
            foreach (var n in NafPremises) all.AddRange(n.VariablesOf());
            return all.Distinct();
        }

        private static ISentence StripUniversals(ISentence s)
        {
            while (s is IComplexSentence { IsQuantifier: true } q &&
                   q.Connective == Connective.LogicSymbol.UNIVERSAL)
                s = q.Children[0];
            return s;
        }

        private static bool CollectConjuncts(ISentence s, List<ISentence> acc, List<ISentence> nafAcc)
        {
            if (s is IComplexSentence c && c.IsConjunction)
                return CollectConjuncts(c.Children[0], acc, nafAcc) && CollectConjuncts(c.Children[1], acc, nafAcc);
            if (s.IsNaf)
            {
                if (!s.Children[0].IsLiteral) return false;
                nafAcc.Add(s.Children[0]);
                return true;
            }
            if (!s.IsLiteral) return false;
            acc.Add(s);
            return true;
        }

        public override string ToString()
        {
            if (IsFact) return Head.ToString();
            var body = Premises.Select(p => p.ToString())
                .Concat(NafPremises.Select(n => "NAF " + n));
            return string.Join(" ∧ ", body) + " ⇒ " + Head;
        }
    }
}
