using System.Linq;
using FirstOrderLogic;

namespace AIPlanning.Planning.GraphPlan {
    public class GpLiteralNode : GpNode {
        public GpLiteralNode(ISentence literal) {
            Literal = literal;
        }

        // Get-only: the hash derives from the literal and nodes live in hash-based sets —
        // swapping the literal after construction would silently corrupt those sets.
        public ISentence Literal { get; }

        public override string ToString() {
            return $"{Literal} [m:{MutexRelations.Aggregate("", (s, m) => $"{s}{m}, ")}]";
        }

        public override int GetHashCode() {
            return Literal.GetHashCode();
        }

        public override bool Equals(object? obj) {
            if (ReferenceEquals(this, obj)) {
                return true;
            }

            return obj is GpLiteralNode otherLiteral && Literal.Equals(otherLiteral.Literal);
        }

        // Inconsistent support (Blum & Furst): every pair of supporting actions is mutex.
        // A lookup on recorded relations suffices because ComputeMutexRelations runs on the action
        // layer BEFORE this literal layer is checked (see ExpandGraph / ExpandBeliefState).
        public bool IsInconsistentSupport(GpLiteralNode other) {
            if (InEdges.Count == 0 || other.InEdges.Count == 0) {
                return false;
            }

            var hasNonMutexSupporterPair = InEdges.Any(inNode => other.InEdges.Any(otherInNode => !inNode.IsMutexWith(otherInNode)));
            return !hasNonMutexSupporterPair;
        }
    }
}