using System;
using System.Collections.Generic;
using System.Linq;

namespace FirstOrderLogic {
    // Immutable, like the rest of the AST: resolution keeps clauses in hash sets, so a clause
    // whose literals could change after construction would be stranded in its bucket.
    public class Clause
    {
        private readonly List<ISentence> _literals;
        private readonly int _hashCode;

        public IReadOnlyList<ISentence> Literals => _literals;

        public Clause(params ISentence[] literals)
        {
            _literals = new List<ISentence>(literals.Length);
            foreach (var literal in literals)
            {
                Add(literal);
            }

            _hashCode = _literals.Aggregate(0, (hash, literal) => hash ^ literal.GetHashCode());
        }

        // Throws on non-literals rather than dropping them: an empty literal list must mean
        // "empty clause", so a malformed sentence must not masquerade as a refutation.
        // A clause is a set — duplicates change nothing semantically and are not kept.
        private void Add(ISentence literal)
        {
            if (!literal.IsLiteral)
            {
                throw new ArgumentException($"{literal} is not a literal", nameof(literal));
            }

            if (_literals.Contains(literal))
            {
                return;
            }

            _literals.Add(literal);
        }

        public Clause With(ISentence literal) => new Clause(_literals.Append(literal).ToArray());

        // Order-insensitive content equality — a clause is a set of literals. The constructor is
        // the only way in and it dedupes, so equal counts plus one-way containment already imply
        // set equality (and with it symmetry).
        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj is not Clause other || _hashCode != other._hashCode || _literals.Count != other._literals.Count)
            {
                return false;
            }

            return _literals.All(other._literals.Contains);
        }

        public override int GetHashCode()
        {
            return _hashCode;
        }

        public override string ToString()
        {
            return _literals.Aggregate("{", (current, lit) => current + lit + ", ")+ "}";
        }
    }
}
