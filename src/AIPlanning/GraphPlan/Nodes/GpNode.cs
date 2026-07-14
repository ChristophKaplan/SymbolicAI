using System.Collections.Generic;

namespace AIPlanning.Planning.GraphPlan {
    public abstract class GpNode {
        public List<GpNode> InEdges { get; } = new();
        public List<GpNode> OutEdges { get; } = new();
        public HashSet<MutexRel> MutexRelation { get; } = new();

        private void AddInEdge(GpNode edge) {
            if (!InEdges.Contains(edge)) {
                InEdges.Add(edge);
            }
        }

        private void AddOutEdge(GpNode edge) {
            if (!OutEdges.Contains(edge)) {
                OutEdges.Add(edge);
            }
        }

        public void ConnectTo(GpNode connectToMe) {
            AddOutEdge(connectToMe);
            connectToMe.AddInEdge(this);
        }

        // Mutex is recorded symmetrically (TryAddMutexRelations), so checking one side suffices.
        public bool IsMutexWith(GpNode other) {
            foreach (var mutex in MutexRelation) {
                if (mutex.ToNode.Equals(other)) {
                    return true;
                }
            }

            return false;
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
            other.MutexRelation.Add(new MutexRel(type, this));
        }
    }
}