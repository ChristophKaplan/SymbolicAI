using System.Collections.Generic;
using System.Linq;

namespace FirstOrderLogic
{
    // Kernel sets (Hansson 1994): B ⊥⊥ α = the minimal subsets of B that entail α — the
    // load-bearing premises of each independent proof path. Entailment delegates to Resolution.
    public class KernelSets
    {
        // One minimal X ⊆ B with X ⊨ α, or null if B ⊭ α.
        public List<ISentence>? FindKernel(IList<ISentence> B, ISentence α)
        {
            if (!Entails(B, α)) return null;
            return Shrink(new List<ISentence>(B), α);
        }

        // All minimal X ⊆ B with X ⊨ α. Complete: any other kernel omits some element of a found
        // kernel, so recursing on B\{e} finds them all.
        public List<List<ISentence>> FindAllKernels(IEnumerable<ISentence> B, ISentence α)
        {
            var results = new List<List<ISentence>>();
            FindAllKernelsRec(new List<ISentence>(B), α, results,
                explored: new HashSet<SentenceSet>(), emitted: new HashSet<SentenceSet>());
            return results;
        }

        private static bool Entails(IList<ISentence> sentences, ISentence target) =>
            new Theory(sentences as List<ISentence> ?? new List<ISentence>(sentences)).Entails(target);

        // Precondition: Entails(sentences, α) == true.
        private List<ISentence> Shrink(List<ISentence> sentences, ISentence α)
        {
            bool changed;
            do
            {
                changed = false;
                for (var i = sentences.Count - 1; i >= 0; i--)
                {
                    var candidate = new List<ISentence>(sentences);
                    candidate.RemoveAt(i);
                    if (!Entails(candidate, α)) continue;
                    sentences = candidate;
                    changed   = true;
                    break;
                }
            }
            while (changed);
            return sentences;
        }

        // `explored` and `emitted` must be separate: a kernel can equal the subset it was found in,
        // and conflating the two would silently drop it.
        private void FindAllKernelsRec(List<ISentence> sentences, ISentence α,
            List<List<ISentence>> results, HashSet<SentenceSet> explored, HashSet<SentenceSet> emitted)
        {
            if (!explored.Add(new SentenceSet(sentences))) return;
            if (!Entails(sentences, α)) return;

            var kernel = Shrink(sentences, α);
            if (emitted.Add(new SentenceSet(kernel))) results.Add(kernel);

            foreach (var e in kernel)
            {
                var without = sentences.Where(s => !s.Equals(e)).ToList();
                FindAllKernelsRec(without, α, results, explored, emitted);
            }
        }

        // Order-independent multiset identity over ISentence value-equality.
        private sealed class SentenceSet
        {
            private readonly List<ISentence> _items;
            private readonly int _hash;

            public SentenceSet(IEnumerable<ISentence> items)
            {
                _items = items.ToList();
                var hash = 0;
                foreach (var s in _items) hash ^= s?.GetHashCode() ?? 0;
                _hash = hash;
            }

            public override int GetHashCode() => _hash;

            public override bool Equals(object obj)
            {
                if (obj is not SentenceSet other) return false;
                if (_items.Count != other._items.Count) return false;

                var matched = new bool[other._items.Count];
                foreach (var item in _items)
                {
                    var found = false;
                    for (var i = 0; i < other._items.Count; i++)
                    {
                        if (matched[i] || !Equals(item, other._items[i])) continue;
                        matched[i] = true;
                        found = true;
                        break;
                    }
                    if (!found) return false;
                }
                return true;
            }
        }
    }
}
