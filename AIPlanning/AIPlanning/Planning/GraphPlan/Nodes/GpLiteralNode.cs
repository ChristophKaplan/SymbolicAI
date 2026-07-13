using System.Linq;
using FirstOrderLogic;

namespace AIPlanning.Planning.GraphPlan {
    public class GpLiteralNode : GpNode {
        public GpLiteralNode(ISentence literal) {
            Literal = literal;
        }

        // Get-only: this node's hash is derived from the literal, and nodes live in hash-based
        // sets — swapping the literal after construction would silently corrupt those sets.
        public ISentence Literal { get; }

        public override string ToString() {
            return $"{Literal} [m:{MutexRelation.Aggregate("", (s, m) => $"{s}{m}, ")}]";
        }

        public override int GetHashCode() {
            return Literal.GetHashCode();
        }

        public override bool Equals(object? obj) {
            if (ReferenceEquals(this, obj)) {
                return true;
            }

            return obj is GpLiteralNode stateNode && Literal.Equals(stateNode.Literal);
        }

        public bool EqualLiteral(GpLiteralNode gpLiteralNode) {
            return Literal.Equals(gpLiteralNode.Literal);
        }

        // Inconsistent support (Blum & Furst): every pair of supporting actions is mutex.
        // The supporters' mutex relations were already recorded by CheckMutexRelations on the
        // action layer (which runs BEFORE this literal layer is checked — see ExpandGraph /
        // ExpandBeliefState), so a lookup on the recorded relations suffices; re-running the
        // full effect/precondition scans via GetMutexType would recompute the same answer.
        public bool IsInconsistentSupport(GpLiteralNode other) {
            if (InEdges.Count == 0 || other.InEdges.Count == 0) {
                return false;
            }

            var isAPossibleWay = InEdges.Any(inNode => other.InEdges.Any(otherInNode => !AreMutex(inNode, otherInNode)));
            return !isAPossibleWay;
        }

        // Mutex is recorded symmetrically (GpNode.TryAddMutexRelations), one side suffices.
        // A node shared by both literals has no relation to itself and counts as non-mutex,
        // matching GetMutexType's Equals short-circuit.
        private static bool AreMutex(GpNode a, GpNode b) {
            return a.MutexRelation.Any(m => m.ToNode.Equals(b));
        }
    }
}