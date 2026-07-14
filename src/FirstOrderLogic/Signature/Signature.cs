using System;
using System.Collections.Generic;
using System.Linq;

namespace FirstOrderLogic
{
    public sealed class Signature
    {
        public sealed class Symbol
        {
            public string Name { get; }
            public int Arity { get; }

            public Symbol(string name, int arity)
            {
                Name = name;
                Arity = arity;
            }

            public static implicit operator string(Symbol symbol) => symbol.Name;
            public override string ToString() => Name;

            public string Of(params string[] args)
            {
                RequireArity(args);
                return args.Length == 0 ? Name : $"{Name}({string.Join(", ", args)})";
            }

            // Classifies each argument exactly as the parser does, so Ground-built atoms stay
            // Equals-identical to their parsed counterparts even for the x/y/z/w names the
            // grammar always reads as variables.
            public Predicate Ground(params string[] args)
            {
                RequireArity(args);
                return new Predicate(Name, args.Select(FirstOrderLogic.TermFor).ToArray());
            }

            // Ground's open sibling: builds a predicate over arbitrary terms, so queries
            // with variables can be formed (e.g. for ForwardChaining.Answers).
            public Predicate Applied(params Term[] args)
            {
                if (args.Length != Arity)
                {
                    throw new ArgumentException(
                        $"{Name}/{Arity} cannot be applied to {args.Length} argument(s).");
                }

                return new Predicate(Name, args);
            }

            private void RequireArity(string[] args)
            {
                if (args.Length != Arity)
                {
                    throw new ArgumentException(
                        $"{Name}/{Arity} cannot be applied to {args.Length} argument(s).");
                }
            }
        }

        private readonly Dictionary<string, int> _predicates;
        private readonly Dictionary<string, int> _functions;

        public Signature(
            IDictionary<string, int>? predicates = null,
            IDictionary<string, int>? functions = null)
        {
            _predicates = predicates != null
                ? new Dictionary<string, int>(predicates)
                : new Dictionary<string, int>();
            _functions = functions != null
                ? new Dictionary<string, int>(functions)
                : new Dictionary<string, int>();
        }

        public IReadOnlyDictionary<string, int> Predicates => _predicates;
        public IReadOnlyDictionary<string, int> Functions  => _functions;

        public IEnumerable<string> Constants => _functions.Where(kv => kv.Value == 0).Select(kv => kv.Key);

        public bool HasPredicate(string symbol) => _predicates.ContainsKey(symbol);
        public bool HasPredicate(string symbol, int arity) =>
            _predicates.TryGetValue(symbol, out var a) && a == arity;

        public bool HasFunction(string symbol) => _functions.ContainsKey(symbol);
        public bool HasConstant(string symbol) =>
            _functions.TryGetValue(symbol, out var a) && a == 0;

        public List<string> UndeclaredPredicates(ISentence sentence)
        {
            var missing = new List<string>();
            Collect(sentence, missing);
            return missing;
        }

        public bool Covers(ISentence sentence) => UndeclaredPredicates(sentence).Count == 0;

        // A bare identifier parses to a Proposition, i.e. a 0-ary predicate — the very shape a
        // misspelling takes, so it has to be checked like any other atom. TRUE/FALSE are logical
        // constants rather than symbols of the signature.
        private void Collect(ISentence sentence, List<string> missing)
        {
            if (sentence.IsLiteral)
            {
                switch (sentence.AtomOf())
                {
                    case IPredicate predicate when !HasPredicate(predicate.Symbol, predicate.Arity):
                        missing.Add($"{predicate.Symbol}/{predicate.Arity}");
                        break;
                    case IProposition { IsNullaryConstant: false } proposition
                        when !HasPredicate(proposition.Symbol, 0):
                        missing.Add($"{proposition.Symbol}/0");
                        break;
                }

                return;
            }

            foreach (var child in sentence.Children)
            {
                Collect(child, missing);
            }
        }

        public sealed class Builder
        {
            private readonly Dictionary<string, int> _predicates = new();
            private readonly Dictionary<string, int> _functions  = new();

            public Builder Predicate(string symbol, int arity)
            {
                _predicates[symbol] = arity;
                return this;
            }

            public Builder Predicate(Symbol symbol) => Predicate(symbol.Name, symbol.Arity);

            public Builder Constant(string symbol)
            {
                _functions[symbol] = 0;
                return this;
            }

            public Builder Constant(Symbol symbol) => Constant(symbol.Name);

            public Builder Function(string symbol, int arity)
            {
                _functions[symbol] = arity;
                return this;
            }

            public Builder Function(Symbol symbol) => Function(symbol.Name, symbol.Arity);

            public Signature Build() => new Signature(_predicates, _functions);
        }
    }
}
