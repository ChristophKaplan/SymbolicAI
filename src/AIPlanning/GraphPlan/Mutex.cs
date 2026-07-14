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

    public readonly struct MutexRel : IEquatable<MutexRel> {
        public readonly MutexType Type;
        public readonly GpNode ToNode;

        public MutexRel(MutexType type, GpNode toNode) : this() {
            Type = type;
            ToNode = toNode;
        }

        public bool Equals(MutexRel other) {
            return Type == other.Type && ToNode.Equals(other.ToNode);
        }

        public override bool Equals([NotNullWhen(true)] object? obj) {
            return obj is MutexRel other && Equals(other);
        }

        public override int GetHashCode() {
            return HashCode.Combine(Type, ToNode);
        }

        public override string ToString() {
            return Type.ToString();
        }
    }
}