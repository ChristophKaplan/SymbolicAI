using System.Collections.Generic;

namespace AIPlanning.Planning.GraphPlan {
    public abstract class GpNode {
        // Lists keep deterministic iteration order; the sets make membership O(1).
        private readonly List<GpNode> _inEdges = new();
        private readonly List<GpNode> _outEdges = new();
        private readonly HashSet<GpNode> _inEdgeSet = new();
        private readonly HashSet<GpNode> _outEdgeSet = new();
        private readonly HashSet<GpNode> _mutexPartners = new();

        public IReadOnlyList<GpNode> InEdges => _inEdges;
        public IReadOnlyList<GpNode> OutEdges => _outEdges;
        public HashSet<MutexRel> MutexRelation { get; } = new();

        public void ConnectTo(GpNode connectToMe) {
            if (_outEdgeSet.Add(connectToMe)) {
                _outEdges.Add(connectToMe);
            }

            if (connectToMe._inEdgeSet.Add(this)) {
                connectToMe._inEdges.Add(this);
            }
        }

        public void DisconnectFrom(GpNode node) {
            if (_outEdgeSet.Remove(node)) {
                _outEdges.Remove(node);
            }

            if (node._inEdgeSet.Remove(this)) {
                node._inEdges.Remove(this);
            }
        }

        public bool HasInEdge(GpNode node) {
            return _inEdgeSet.Contains(node);
        }

        public bool HasOutEdge(GpNode node) {
            return _outEdgeSet.Contains(node);
        }

        // Mutex is recorded symmetrically (TryAddMutexRelations), so checking one side suffices.
        public bool IsMutexWith(GpNode other) {
            return _mutexPartners.Contains(other);
        }

        public MutexType GetMutexType(GpNode other) {
            if (Equals(other)) {
                return MutexType.None;
            }

            if (this is GpLiteralNode s1 && other is GpLiteralNode s2) {
                if (s1.IsInconsistentSupport(s2)) {
                    return MutexType.InconsistentSupport;
                }
                else if (s1.Literal.IsNegationOf(s2.Literal)) {
                    return MutexType.LiteralNegation;
                }
            }

            if (this is GpActionNode a1 && other is GpActionNode a2) {
                if (a1.IsInconsistentEffects(a2)) {
                    return MutexType.InconsistentEffects;
                }
                else if (a1.IsInterference(a2)) {
                    return MutexType.Interference;
                }
                else if (a1.IsCompetingNeeds(a2)) {
                    return MutexType.CompetingNeeds;
                }
            }

            return MutexType.None;
        }

        public void TryAddMutexRelations(GpNode other, MutexType type) {
            MutexRelation.Add(new MutexRel(type, other));
            _mutexPartners.Add(other);
            other.MutexRelation.Add(new MutexRel(type, this));
            other._mutexPartners.Add(this);
        }
    }
}
