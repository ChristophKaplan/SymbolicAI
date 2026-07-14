using System;
using System.Collections.Generic;
using System.Linq;

namespace FirstOrderLogic {
    public class SatSolvers {
        readonly Random _random = new();
        public PossibleWorld? WalkSAT(List<Clause> clauses, float p, int maxFlips) {
            if(!IsPropositional(clauses)) throw new Exception("WalkSAT only works with propositional logic");

            if (clauses.Any(clause => clause.Literals.Count == 0)) return null;

            var model = new PossibleWorld(GetRandomAssigmentFor(clauses));

            for (var i = 0; i < maxFlips; i++) {
                var eval = model.Evaluate(clauses);
                if (eval) return model;
            
                var clause = GetRandomUnsatisfiedClause(clauses, model);
         
                if (_random.NextDouble() < p) {
                    var literal = clause.Literals[_random.Next(0, clause.Literals.Count)];
                    model.Switch(literal.GetProposition());
                } else {
                    var bestModel = model.Clone();
                    var bestModelScore = 0;
                    foreach (var literal in clause.Literals) {
                        var newModel = model.Clone();
                        newModel.Switch(literal.GetProposition());
                        var newModelScore = clauses.Count(cls => newModel.Evaluate(cls));
                        if (newModelScore > bestModelScore) {
                            bestModel = newModel;
                            bestModelScore = newModelScore;
                        }
                    }
                
                    model = bestModel;
                }
            }

            return !model.Evaluate(clauses) ? null : model;
        }

        private bool IsPropositional(List<Clause> clauses) {
            return clauses.All(clause => clause.Literals.All(lit => lit.IsPropositional()));
        }

        private Clause GetRandomUnsatisfiedClause(List<Clause> clauses, PossibleWorld model) {
            var unsatisfiedClauses = clauses.Where(clause => !model.Evaluate(clause)).ToList();
            return unsatisfiedClauses[_random.Next(0, unsatisfiedClauses.Count)];
        }
    
        private Dictionary<IProposition, bool> GetRandomAssigmentFor(List<Clause> clauses) {
            var assignment = new Dictionary<IProposition, bool>();
            foreach (var clause in clauses) {
                foreach (var literal in clause.Literals) {
                    var proposition = literal.GetProposition();
                    assignment.TryAdd(proposition, _random.Next(0, 2) == 1);
                }
            }
        
            return assignment;
        }
    }
}