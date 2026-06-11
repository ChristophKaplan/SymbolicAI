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

        public bool IsInconsistentSupport(GpLiteralNode other) {
            if (InEdges.Count == 0 || other.InEdges.Count == 0) {
                return false;
            }

            var isAPossibleWay = InEdges.Any(inNode => other.InEdges.Any(otherInNode => inNode.GetMutexType(otherInNode) == MutexType.None));
            return !isAPossibleWay;
        }
    }
}