using System.Collections.Generic;

namespace FirstOrderLogic
{
    internal sealed class Substitution
    {
        public static readonly Substitution Empty = new(new Dictionary<Variable, Term>());

        private readonly IReadOnlyDictionary<Variable, Term> _map;

        public Substitution(IReadOnlyDictionary<Variable, Term> map) => _map = map;

        // Resolves a term to its final binding, following variable chains (x -> y -> a). The
        // unifier's occurs-check rules out cycles, so the recursion terminates.
        public Term Walk(Term term)
        {
            switch (term)
            {
                case Variable v:
                    return _map.TryGetValue(v, out var bound) ? Walk(bound) : v;
                case Function f when f.Arity > 0:
                    var args = new Term[f.Terms.Count];
                    for (var i = 0; i < args.Length; i++)
                    {
                        args[i] = Walk(f.Terms[i]);
                    }

                    return new Function(f.TermSymbol, args);
                default:
                    return term;
            }
        }

        public ISentence Apply(ISentence literal)
        {
            var result = literal;
            foreach (var v in literal.VariablesOf())
            {
                var resolved = Walk(v);
                if (!resolved.Equals(v))
                {
                    result = result.Substitute(v, resolved);
                }
            }

            return result;
        }

        // Union with `more`. Callers only add bindings for variables absent here, so the union is
        // conflict-free by construction.
        public Substitution Extend(IReadOnlyDictionary<Variable, Term> more)
        {
            var merged = new Dictionary<Variable, Term>(_map.Count + more.Count);
            foreach (var pair in _map)
            {
                merged[pair.Key] = pair.Value;
            }

            foreach (var pair in more)
            {
                merged[pair.Key] = pair.Value;
            }

            return new Substitution(merged);
        }
    }
}
