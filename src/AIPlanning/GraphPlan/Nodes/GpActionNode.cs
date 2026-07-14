using System;
using System.Linq;

namespace AIPlanning.Planning.GraphPlan {
    public class GpActionNode : GpNode {
        private int _useCount = 0;

        public GpActionNode(GpAction gpAction, bool isPersistenceAction = false) {
            IsPersistenceAction = isPersistenceAction;
            GpAction = gpAction;
        }

        public bool IsPersistenceAction { get; }
        public GpAction GpAction { get; }

        public bool TryIncreaseUseCount(int useCountStop) {
            if (_useCount >= useCountStop) {
                return false;
            }

            _useCount++;
            return true;
        }

        public bool IsInconsistentEffects(GpActionNode other) {
            return GpAction.Effects.Any(effect => other.GpAction.Effects.Any(otherEffect => effect.IsNegationOf(otherEffect)));
        }

        public bool IsInterference(GpActionNode other) {
            var isInterference = GpAction.Effects.Any(effect => other.GpAction.Preconditions.Any(otherPreCon => effect.IsNegationOf(otherPreCon))) ||
                                 other.GpAction.Effects.Any(effect => GpAction.Preconditions.Any(preCon => effect.IsNegationOf(preCon)));
            return isInterference;
        }

        // Competing-Needs (Russell/Norvig): mutex if any precondition of the one is mutex, at the
        // previous literal level, with any precondition of the other. Uses the already-computed
        // relations on the precondition nodes via InEdges instead of re-checking literal pairs.
        public bool IsCompetingNeeds(GpActionNode other) {
            foreach (var inNode in InEdges) {
                foreach (var mutex in inNode.MutexRelation) {
                    if (other.InEdges.Contains(mutex.ToNode)) {
                        return true;
                    }
                }
            }

            return false;
        }
    
        public override int GetHashCode() {
            return HashCode.Combine(GpAction, IsPersistenceAction);
        }

        public override bool Equals(object? obj) {
            if(ReferenceEquals(this,obj)) {
                return true;
            }
        
            if (obj is GpActionNode actionNode) {
                return  GpAction.Equals(actionNode.GpAction) && IsPersistenceAction == actionNode.IsPersistenceAction;
            }

            return false;
        }
    
        public override string ToString() {
            return $"{GpAction} ";
        }
    }
}