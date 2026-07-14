using System.Collections.Generic;
using System.Linq;

namespace AIPlanning.Planning.GraphPlan {
    public abstract class GpNodeSet<TNode> where TNode : GpNode {
        private readonly List<TNode> _nodes;

        protected GpNodeSet() {
            _nodes = new List<TNode>();
        }

        protected GpNodeSet(IEnumerable<GpNode> nodes) {
            _nodes = nodes.Cast<TNode>().ToList();
        }

        public IReadOnlyList<TNode> Nodes => _nodes;

        // Returns the canonical node stored in the set; callers MUST connect edges to the
        // returned instance, not to their input.
        public TNode Add(TNode node) {
            var contained = _nodes.FirstOrDefault(node.Equals);
            if (contained != null) {
                return contained;
            }

            _nodes.Add(node);
            return node;
        }

        public override int GetHashCode() {
            var hash = Nodes.Count;
            foreach (var node in Nodes) {
                hash ^= node.GetHashCode();
            }

            return hash;
        }

        public override bool Equals(object? obj) {
            if (ReferenceEquals(this, obj)) {
                return true;
            }

            if (obj is not GpNodeSet<TNode> other) {
                return false;
            }

            return Nodes.MultisetEquals(other.Nodes);
        }
    }
}
