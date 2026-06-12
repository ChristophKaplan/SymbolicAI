using System;
using System.Collections.Generic;
using System.Linq;
using LogHelper;
using LRParser.Language;

namespace FirstOrderLogic {
    public static class FirstOrderLogicExtensions
    {
        public static Connective.LogicSymbol ToLogicalConstant(this LexValue lexValue) {
            switch (lexValue.Value) {
                case "OR":
                case "||":
                case "\u2228":
                    return Connective.LogicSymbol.DISJUNCTION;
                case "AND":
                case "&&":
                case "\u2227":
                    return Connective.LogicSymbol.CONJUNCTION;
                case "NOT":
                case "!":
                case "-":
                case "~":
                case "\u00ac":
                    return Connective.LogicSymbol.NEGATION;
                case "IFF":
                case "<=>":
                case "\u21d4":
                    return Connective.LogicSymbol.BICONDITIONAL;
                case "IMPLIES":
                case "=>":
                case "\u21d2":
                    return Connective.LogicSymbol.IMPLICATION;
                case "TRUE":
                case "\u22a4":
                    return Connective.LogicSymbol.TRUE;
                case "FALSE":
                case "\u22a5":
                    return Connective.LogicSymbol.FALSE;
                case "FORALL":
                case "\u2200":
                    return Connective.LogicSymbol.UNIVERSAL;
                case "EXISTS":
                case "\u2203":
                    return Connective.LogicSymbol.EXISTENTIAL;
                
                default:
                    throw new Exception($"Unknown Logic Symbol: {lexValue}");
            }
        }
    
        public static ISentence ConnectSentences(this FirstOrderLogic logic, List<ISentence> sentences, Connective.LogicSymbol connective = Connective.LogicSymbol.CONJUNCTION) {
            switch (sentences.Count) {
                case 0:
                    throw new Exception("No sentences to connect.");
                case 1:
                    return sentences[0];
            }

            var conjunct = new ComplexSentence(sentences[0], connective, sentences[1]);
            for (var i = 2; i < sentences.Count; i++) {
                conjunct = new ComplexSentence(conjunct,connective, sentences[i]);
            }

            return conjunct;
        }
    
        private delegate void TransformationDelegate(ref ISentence sentence);

        public static ISentence ToPrenexForm(this FirstOrderLogic logic, ISentence sentence, out List<ISentence> steps) {
            steps = new List<ISentence>();
            return ToPrenexFormCore(sentence, steps);
        }

        // Trace-free overload: skips the per-step clones that dominate normalisation cost.
        public static ISentence ToPrenexForm(this FirstOrderLogic logic, ISentence sentence) =>
            ToPrenexFormCore(sentence, null);

        private static ISentence ToPrenexFormCore(ISentence sentence, List<ISentence>? steps) {
            var clone = sentence.Clone();

            var transformations = new List<TransformationDelegate> {
                (ref ISentence s) => TransformationFOL.Transform(TransformationFOL.EquivType.SimplifyConstants, ref s),
                (ref ISentence s) => TransformationFOL.Transform(TransformationFOL.EquivType.DissolveBiconditional, ref s),
                (ref ISentence s) => TransformationFOL.Transform(TransformationFOL.EquivType.DissolveImplication, ref s),
                (ref ISentence s) => TransformationFOL.Transform(TransformationFOL.EquivType.PushNegation, ref s),
                (ref ISentence s) => TransformationFOL.Transform(TransformationFOL.EquivType.DoubleNegation, ref s),
                (ref ISentence s) => TransformationFOL.Transform(TransformationFOL.EquivType.Absorption, ref s),
                (ref ISentence s) => TransformationFOL.Transform(TransformationFOL.EquivType.AssociationAndIdem, ref s),
                (ref ISentence s) => TransformationFOL.Transform(TransformationFOL.EquivType.PullQuantifier, ref s),
                (ref ISentence s) => TransformationFOL.Transform(TransformationFOL.EquivType.RemoveDuplicateQuantifier, ref s),
            };

            ApplyUntilStable(ref clone, transformations, steps);
            return clone;
        }

        private static void ApplyUntilStable(
            ref ISentence clone, List<TransformationDelegate> transformations, List<ISentence>? steps) {
            while (true) {
                var start = clone.Clone();
                foreach (var transform in transformations) {
                    transform(ref clone);
                    steps?.Add(clone.Clone());
                }
                if (start.Equals(clone)) {
                    break;
                }
            }
        }

        public static ISentence ToConjunctiveNormalForm(this FirstOrderLogic logic, ISentence sentence, out List<ISentence> steps) {
            steps = new List<ISentence>();
            return ToConjunctiveNormalFormCore(sentence, steps);
        }

        // Trace-free overload — see ToPrenexForm(logic, sentence).
        public static ISentence ToConjunctiveNormalForm(this FirstOrderLogic logic, ISentence sentence) =>
            ToConjunctiveNormalFormCore(sentence, null);

        private static ISentence ToConjunctiveNormalFormCore(ISentence sentence, List<ISentence>? steps) {
            var clone = ToPrenexFormCore(sentence, steps).Clone();

            var transformations = new List<TransformationDelegate> {
                (ref ISentence s) => TransformationFOL.Transform(TransformationFOL.EquivType.DistributionOfDisjunction, ref s)
            };

            ApplyUntilStable(ref clone, transformations, steps);

            if(!clone.IsCNF()) { throw new Exception("Sentence is not in CNF"); }
            return clone;
        }

        public static ISentence SkolemForm(this FirstOrderLogic logic, ISentence sentence) {
            var clone = sentence.Clone();

            // Expects PNF. Each existential becomes a Skolem term over the universals enclosing
            // it: sk1 if none, sk1(u, …) otherwise.
            var substitution = new Dictionary<Variable, Function>();
            var universalsInScope = new List<Variable>();
            var skolemCounter = 0;

            var current = clone;
            while (current is IComplexSentence { IsQuantifier: true } quantified) {
                var quantifier = (Quantifier)quantified.Connective;
                if (quantifier.Symbol == Connective.LogicSymbol.UNIVERSAL) {
                    universalsInScope.Add(quantifier.Variable);
                }
                else {
                    var args = universalsInScope
                        .Select(v => (Term)new Variable(v.TermSymbol))
                        .ToArray();
                    substitution.Add(quantifier.Variable, new Function($"sk{++skolemCounter}", args));
                }

                current = quantified.Children[0];
            }

            foreach (var variable in substitution.Keys) {
                clone.SubstituteTerm(variable, substitution[variable]);
            }

            TransformationFOL.Transform(TransformationFOL.EquivType.RemoveQuantifier, ref clone);

            return clone;
        }
    
        public static List<Clause> GetClauseSet(this ISentence sentence, List<Clause>? clauseSet = null) {
            if (!sentence.IsCNF()) { throw new Exception("Sentence is not in CNF"); }
        
            clauseSet ??= new List<Clause>();

            if(sentence.IsDisjunctionOfLiterals())
            {
                // Only the clause leaves need a defensive copy; interior conjunction nodes don't.
                var clauseList = sentence.Clone().GetLiterals();
                clauseSet.Add(new Clause(clauseList.ToArray()));
                return clauseSet;
            }
        
            foreach (var child in sentence.Children)
            {
                child.GetClauseSet(clauseSet);
            }
        
            return clauseSet;
        }

        public static bool TryGetBinary(
            this ISentence sentence, string symbol, string firstTerm, out string? secondTerm) {
            secondTerm = null;
            if (sentence is not IPredicate wrapper) return false;

            var pred = wrapper.GetPredicate();
            if (pred.Symbol != symbol) return false;

            var terms = pred.Terms;
            if (terms == null || terms.Length < 2) return false;
            if (terms[0].ToString() != firstTerm) return false;

            secondTerm = terms[1].ToString();
            return !string.IsNullOrEmpty(secondTerm);
        }

        public static List<ISentence> GetInstancesOverTime(this ISentence sentence, int from, int to) {
            var sentences = new List<ISentence>();
            for (var i = from; i < to; i++) {
                var clone = sentence.Clone();
                clone.AddTime(i);
                sentences.Add(clone);
            }

            return sentences;
        }
    }
}