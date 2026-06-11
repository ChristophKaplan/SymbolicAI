using System;
using FirstOrderLogic;

namespace FirstOrderLogicExample {
    class Program {
        static void Main(string[] args) {
            var logic = new FirstOrderLogic.FirstOrderLogic();

            var sentence = (ISentence)logic.TryParse("(Human(Sokrates) AND (FORALL x (Human(x) => Mortal(x))))");
            var prenexForm = logic.ToPrenexForm(sentence, out var steps);
            var skolemForm = logic.SkolemForm(prenexForm);
            var consequence = (ISentence)logic.TryParse("Mortal(Sokrates)");
            var resolution = new Resolution();

            var result = resolution.Resolve(skolemForm, consequence);

            Console.WriteLine(result);
        }
    }
}