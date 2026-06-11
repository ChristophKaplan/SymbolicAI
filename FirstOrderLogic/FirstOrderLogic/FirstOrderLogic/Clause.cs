using System;
using System.Collections.Generic;
using System.Linq;

namespace FirstOrderLogic {
    public class Clause
    {
        public List<ISentence> Literals { get; } = new();
    
        public Clause(params ISentence[] literals)
        {
            if (literals.Any(t => !t.IsLiteral)) return;
            Literals = new List<ISentence>(literals);
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

            if (this is Resolvent)
            {
                output += ResolventAsString() + "\n";
            }
            else
            {
                output += "literal:\n" + ToString() + "\n";
            }

            return output;
        }
    }
}