using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using FirstOrderLogic;

namespace AIPlanning.Planning.GraphPlan {
    public static class HelperExtensions {
        public static bool Match(this ISentence sentence, ISentence other, [NotNullWhen(true)] out Unificator? unificator) {
            // Argument order is deliberate: the returned unifier must bind in the direction
            // callers (e.g. SpecifyAction) rely on.
            return Unificator.TryMatch(other, sentence, out unificator);
        }

        public static bool MultisetEquals<T>(this IReadOnlyList<T> left, IReadOnlyList<T> right) where T : notnull {
            if (left.Count != right.Count) {
                return false;
            }

            var counts = new Dictionary<T, int>();
            foreach (var item in left) {
                counts.TryGetValue(item, out var count);
                counts[item] = count + 1;
            }

            foreach (var item in right) {
                if (!counts.TryGetValue(item, out var count) || count == 0) {
                    return false;
                }

                counts[item] = count - 1;
            }

            return true;
        }

        public static List<GpNode> GetConflictFreeSubset(this IReadOnlyList<GpNode> nodes) {
            return nodes.Where(node => !node.HasConflictIn(nodes)).ToList();
        }

        public static bool IsConflictFree(this IReadOnlyList<GpNode> nodes) {
            return !nodes.Any(node => node.HasConflictIn(nodes));
        }

        private static bool HasConflictIn(this GpNode node, IReadOnlyList<GpNode> nodes) {
            return node.MutexRelation.Any(mutexTo => nodes.Contains(mutexTo.ToNode));
        }

        public static void CheckMutexRelations(this IReadOnlyList<GpNode> nodes) {
            for (var i = 0; i < nodes.Count; i++) {
                for (var j = i + 1; j < nodes.Count; j++) {
                    var nodeA = nodes[i];
                    var nodeB = nodes[j];
                    var mutexType = nodeA.GetMutexType(nodeB);
                    if (mutexType != MutexType.None) {
                        nodeA.TryAddMutexRelations(nodeB, mutexType);
                    }
                }
            }
        }

        public static List<List<T>> GetCombinations<T>(this List<List<T>> lists) {
            IEnumerable<IEnumerable<T>> emptyProduct = new[] {Enumerable.Empty<T>()};
            return lists.Aggregate(emptyProduct,
                    (accumulator, sequence) => from accseq in accumulator from item in sequence select accseq.Concat(new[] {item}))
                .Select(l => l.ToList()).ToList();
        }
    }
}