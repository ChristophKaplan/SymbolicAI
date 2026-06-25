using System.Collections.Generic;
using System.Linq;

namespace FirstOrderLogic
{
    // Abduction over rules: the minimal sets of assumed ground literals (over the abducible
    // predicates) that make the observation derivable without introducing new conflicts.
    public class AbductiveChaining
    {
        private readonly int _maxDepth;

        public AbductiveChaining(int maxDepth = 500)
        {
            _maxDepth = maxDepth;
        }

        public List<List<ISentence>> Explain(
            IEnumerable<ISentence> kb, ISentence observation, IEnumerable<string> abduciblePredicates)
        {
            if (!observation.IsLiteral) return new List<List<ISentence>>();

            var sentences = kb.ToList();
            var candidates = BackwardChaining.Prove(
                    Rule.FromAll(sentences), new List<ISentence> { observation },
                    new Dictionary<Variable, Term>(), new List<ISentence>(),
                    0, new BackwardChaining.Counter(),
                    new HashSet<string>(abduciblePredicates), _maxDepth)
                .ToList();

            HashSet<string>? baseline = null;
            var consistent = candidates.Where(h =>
            {
                if (h.Count == 0) return true;
                baseline ??= ConflictKeys(sentences);
                return !ConflictKeys(sentences.Concat(h)).Except(baseline).Any();
            }).ToList();

            return Minimal(consistent);
        }

        private static HashSet<string> ConflictKeys(IEnumerable<ISentence> kb) =>
            Theory.FindAllConflicts(ForwardChaining.Saturate(kb))
                .Select(c => c.Claim.ToString())
                .ToHashSet();

        private static List<List<ISentence>> Minimal(List<List<ISentence>> sets)
        {
            var keyed = sets
                .Select(s => (set: s, key: s.Select(x => x.ToString()).ToHashSet()))
                .ToList();

            var result = new List<List<ISentence>>();
            var emitted = new List<HashSet<string>>();
            foreach (var (set, key) in keyed.OrderBy(p => p.key.Count))
            {
                if (emitted.Any(e => e.IsSubsetOf(key))) continue;
                emitted.Add(key);
                result.Add(set);
            }
            return result;
        }
    }
}
