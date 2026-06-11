using System;
using System.Diagnostics.CodeAnalysis;

namespace AIPlanning.Planning.GraphPlan {
    public enum MutexType {
        None,
        InconsistentSupport,
        LiteralNegation,
        Interference,
        CompetingNeeds,
        InconsistentEffects
    }

//Mainly used for debugging the mutex relations
    public readonly struct MutexRel {
        public readonly MutexType Type;
        public readonly GpNode ToNode;

        public MutexRel(MutexType type, GpNode toNode) : this() {
            Type = type;
            ToNode = toNode;
        }

        public override bool Equals([NotNullWhen(true)] object? obj) {
            return obj is MutexRel other && Type == other.Type && ToNode.Equals(other.ToNode);
        }

        public override int GetHashCode() {
            return HashCode.Combine(Type, ToNode);
        }

        public override string ToString() {
            return Type.ToString();
        }
    }
}