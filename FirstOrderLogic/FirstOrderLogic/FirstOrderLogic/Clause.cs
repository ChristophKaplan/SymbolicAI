using System;
using System.Collections.Generic;
using System.Linq;

namespace FirstOrderLogic {
    public class Clause
    {
        public List<ISentence> Literals { get; }

        public Clause(params ISentence[] literals)
        {
            // An empty literal list must mean "empty clause": silently dropping bad input here
            // would let a malformed sentence masquerade as a refutation.
            var nonLiteral = literals.FirstOrDefault(t => !t.IsLiteral);
            if (nonLiteral != null)
            {
                throw new ArgumentException($"{nonLiteral} is not a literal", nameof(literals));
            }

            // A clause is a set: duplicates change nothing semantically and must not survive
            // construction (AddLiteral already dedups).
            Literals = new List<ISentence>(literals.Length);
            foreach (var literal in literals)
            {
                if (!Literals.Contains(literal)) Literals.Add(literal);
            }
        }
    
        public void AddLiteral(ISentence literal)
        {
            if (!literal.IsLiteral)
            {
                throw new Exception($"{literal} is not a literal");
            }

            if (Literals.Contains(literal))
            {
                return;
            }

            Literals.Add(literal);
        }
    
        public override string ToString()
        {
            return Literals.Aggregate("{", (current, lit) => current + lit + ", ")+ "}";
        }
    }

    public class Resolvent : Clause {
        private readonly Clause _clauseA;
        private readonly Clause _clauseB;
        public bool IsEmptyClause() => Literals.Count == 0;

        public Resolvent(Clause clause1, Clause clause2, params ISentence[] literals) : base(literals) {
            _clauseA = clause1;
            _clauseB = clause2;
        }
    
        public string ResolventAsString() {
            var s = "";
            s += $"C_A: {_clauseA}\n";
            s += $"C_B: {_clauseB}\n";
            s += "-----------------------\n";
            s += $"Res: {ToString()}\n";
            return s;
        }
    
        public string TraceResolution() {
            var output = "";

            if (_clauseA is Resolvent parent1) {
                output += parent1.TraceResolution() + "\n";
            }

            if (_clauseB is Resolvent parent2) {
                output += parent2.TraceResolution() + "\n";
            }

            output += ResolventAsString() + "\n";

            return output;
        }
    }
}