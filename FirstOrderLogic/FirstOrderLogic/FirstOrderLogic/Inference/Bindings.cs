using System.Collections.Generic;
using System.Linq;

namespace FirstOrderLogic
{
    // Substitution plumbing shared by the inference procedures. Internal — not part of the public
    // FOL vocabulary (which exposes Unificator for one-shot unification).
    internal static class Bindings
    {
        // The unifier's occurs-check rules out cyclic bindings, so the recursion terminates.
        public static Term Walk(Term term, IReadOnlyDictionary<Variable, Term> theta)
        {
            switch (term)
            {
                case Variable v:
                    return theta.TryGetValue(v, out var bound) ? Walk(bound, theta) : v;
                case Function f when f.Arity > 0:
                    var args = new Term[f.Terms.Length];
                    for (var i = 0; i < args.Length; i++) args[i] = Walk(f.Terms[i], theta);
                    return new Function(f.TermSymbol, args);
                default:
                    return term;
            }
        }

        // Returns a fresh clone; never mutates the input.
        public static ISentence Apply(ISentence literal, IReadOnlyDictionary<Variable, Term> theta)
        {
            var clone = literal.Clone();
            if (theta.Count == 0) return clone;
            foreach (var v in VariablesOf(clone).ToList())
            {
                var resolved = Walk(v, theta);
                if (!resolved.Equals(v)) clone.SubstituteTerm(v, resolved);
            }
            return clone;
        }

        // Callers only extend with bindings for variables absent from theta, so the union is
        // conflict-free by construction.
        public static Dictionary<Variable, Term> Extend(
            IReadOnlyDictionary<Variable, Term> theta, IReadOnlyDictionary<Variable, Term> more)
        {
            var result = new Dictionary<Variable, Term>(theta.Count + more.Count);
            foreach (var pair in theta) result[pair.Key] = pair.Value;
            foreach (var pair in more) result[pair.Key] = pair.Value;
            return result;
        }

        public static IEnumerable<Variable> VariablesOf(ISentence literal)
        {
            var atom = literal.IsNegation ? literal.Children[0] : literal;
            if (atom is not IPredicate predicate) return Enumerable.Empty<Variable>();
            return predicate.GetVariables().Distinct();
        }

        public static bool TryUnify(ISentence a, ISentence b, out Dictionary<Variable, Term> mgu)
        {
            var unificator = new Unificator(a, b);
            if (!unificator.IsUnifiable)
            {
                mgu = new Dictionary<Variable, Term>();
                return false;
            }
            mgu = new Dictionary<Variable, Term>(unificator.Substitutions);
            return true;
        }

        // Polarity is part of the tag: Unificator unifies the underlying atoms regardless of
        // negation, so this pre-filter is what keeps a positive literal from matching a negative one.
        public static string Signature(ISentence literal)
        {
            var atom = literal.IsNegation ? literal.Children[0] : literal;
            var tag = atom is IPredicate predicate
                ? predicate.Symbol + "/" + predicate.Arity
                : ((IAtomicSentence)atom).Symbol + "/0";
            return literal.IsNegation ? "¬" + tag : tag;
        }

        public static string SymbolOf(ISentence literal)
        {
            var atom = literal.IsNegation ? literal.Children[0] : literal;
            return atom is IPredicate predicate ? predicate.Symbol : ((IAtomicSentence)atom).Symbol;
        }
    }
}
