using System;
using System.Collections.Generic;
using System.Linq;

namespace FirstOrderLogic {
    public class Clause
    {
        public List<ISentence> Literals { get; }

        public Clause(params ISentence[] literals)
        {
            Literals = new List<ISentence>(literals.Length);
            foreach (var literal in literals)
            {
                AddLiteral(literal);
            }
        }

        // Throws on non-literals rather than dropping them: an empty literal list must mean
        // "empty clause", so a malformed sentence must not masquerade as a refutation.
        // A clause is a set — duplicates change nothing semantically and are not kept.
        public void AddLiteral(ISentence literal)
        {
            if (!literal.IsLiteral)
            {
                throw new ArgumentException($"{literal} is not a literal", nameof(literal));
            }

            if (Literals.Contains(literal))
            {
                return;
            }

            Literals.Add(literal);
        }

        // Order-insensitive content equality — a clause is a set of literals.
        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj is not Clause other || Literals.Count != other.Literals.Count)
            {
                return false;
            }

            return Literals.All(other.Literals.Contains);
        }

        public override int GetHashCode()
        {
            var hash = 0;
            foreach (var literal in Literals)
            {
                hash ^= literal.GetHashCode();
            }

            return hash;
        }

        public override string ToString()
        {
            return Literals.Aggregate("{", (current, lit) => current + lit + ", ")+ "}";
        }
    }
}
