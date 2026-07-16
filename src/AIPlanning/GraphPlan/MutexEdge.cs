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

    public readonly struct MutexEdge : IEquatable<MutexEdge> {
        public readonly MutexType Type;
        public readonly GpNode ToNode;

        public MutexEdge(MutexType type, GpNode toNode) : this() {
            Type = type;
            ToNode = toNode;
        }

        public bool Equals(MutexEdge other) {
            return Type == other.Type && ToNode.Equals(other.ToNode);
        }

        public override bool Equals([NotNullWhen(true)] object? obj) {
            return obj is MutexEdge other && Equals(other);
        }

        public override int GetHashCode() {
            return HashCode.Combine(Type, ToNode);
        }

        public override string ToString() {
            return Type.ToString();
        }
    }
}