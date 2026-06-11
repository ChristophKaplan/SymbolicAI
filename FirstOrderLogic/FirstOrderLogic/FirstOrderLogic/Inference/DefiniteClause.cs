using System;
using System.Collections.Generic;
using System.Linq;

namespace FirstOrderLogic
{
    // A definite (Horn) clause: a conjunction of positive premise literals implying a single positive
    // head literal — p1 ∧ … ∧ pn ⇒ q. A fact is the n = 0 case (just q). All variables are implicitly
    // universally quantified. This is the clause shape ForwardChaining and BackwardChaining reason
    // over; sentences outside it (head negation, a disjunctive/negated body, existentials, …) are not
    // definite clauses, and `From` reports that by returning null.
    public class DefiniteClause
    {
        public IReadOnlyList<ISentence> Premises { get; }
        public ISentence Head { get; }
        public bool IsFact => Premises.Count == 0;

        public DefiniteClause(ISentence head, IReadOnlyList<ISentence> premises)
        {
            Head = head;
            Premises = premises;
        }

        // Reads `sentence` as a definite clause, or returns null if it is not one. Accepts a bare
        // positive atom (a fact), or an implication whose head is a single positive atom and whose body
        // is a conjunction of positive atoms; a leading universal-quantifier prefix is tolerated.
        // Works on a clone, so the source sentence is never mutated or aliased.
        public static DefiniteClause? From(ISentence sentence)
        {
            var s = StripUniversals(sentence.Clone());

            if (IsPositiveAtom(s))
                return new DefiniteClause(s, Array.Empty<ISentence>());

            if (!s.IsImplication) return null;

            var head = s.Children[1];
            if (!IsPositiveAtom(head)) return null;

            var premises = new List<ISentence>();
            return CollectConjuncts(s.Children[0], premises)
                ? new DefiniteClause(head, premises)
                : null;
        }

        // A fresh-variable copy: every variable is renamed with a unique suffix so repeated uses of the
        // same clause (successive rule firings, sibling proof branches) cannot capture each other's
        // variables — the standardize-apart step both chaining procedures depend on for soundness.
        public DefiniteClause Renamed(int id)
        {
            var rename = new Dictionary<Variable, Term>();
            foreach (var v in Variables()) rename[v] = new Variable(v.TermSymbol + "#" + id);
            var head = Bindings.Apply(Head, rename);
            var premises = Premises.Select(p => Bindings.Apply(p, rename)).ToList();
            return new DefiniteClause(head, premises);
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

        private static bool IsPositiveAtom(ISentence s) => s.IsLiteral && !s.IsNegation;

        // Flatten a conjunction tree into its positive-atom leaves. Any non-conjunction, non-positive-
        // atom node (disjunction, negation, quantifier, implication) disqualifies the whole body.
        private static bool CollectConjuncts(ISentence s, List<ISentence> acc)
        {
            if (s is IComplexSentence c && c.IsConjunction)
                return CollectConjuncts(c.Children[0], acc) && CollectConjuncts(c.Children[1], acc);
            if (!IsPositiveAtom(s)) return false;
            acc.Add(s);
            return true;
        }

        public override string ToString() =>
            IsFact ? Head.ToString() : string.Join(" ∧ ", Premises) + " ⇒ " + Head;
    }
}
