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
                explored: new HashSet<string>(), emitted: new HashSet<string>());
            return results;
        }

        private static bool Entails(IList<ISentence> sentences, ISentence target) =>
            new Theory(sentences as List<ISentence> ?? new List<ISentence>(sentences)).Entails(target);

        // Precondition: Entails(sentences, α) == true. Standard single downward-deletion pass:
        // when index i is tested, the set is a superset of the final result, so by monotonicity
        // every survivor is still load-bearing at the end — one pass suffices for minimality.
        private List<ISentence> Shrink(List<ISentence> sentences, ISentence α)
        {
            for (var i = sentences.Count - 1; i >= 0; i--)
            {
                var candidate = new List<ISentence>(sentences);
                candidate.RemoveAt(i);
                if (Entails(candidate, α)) sentences = candidate;
            }
            return sentences;
        }

        // `explored` and `emitted` must be separate: a kernel can equal the subset it was found in,
        // and conflating the two would silently drop it.
        private void FindAllKernelsRec(List<ISentence> sentences, ISentence α,
            List<List<ISentence>> results, HashSet<string> explored, HashSet<string> emitted)
        {
            if (!explored.Add(Key(sentences))) return;
            if (!Entails(sentences, α)) return;

            var kernel = Shrink(sentences, α);
            if (emitted.Add(Key(kernel))) results.Add(kernel);

            foreach (var e in kernel)
            {
                var without = sentences.Where(s => !s.Equals(e)).ToList();
                FindAllKernelsRec(without, α, results, explored, emitted);
            }
        }

        // Order-independent multiset identity; ToString is structural, mirroring ISentence equality.
        private static string Key(IEnumerable<ISentence> sentences) =>
            string.Join("\n", sentences.Select(s => s.ToString()).OrderBy(s => s, System.StringComparer.Ordinal));
    }
}
