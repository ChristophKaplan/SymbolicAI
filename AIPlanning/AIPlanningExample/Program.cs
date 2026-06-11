using AIPlanning.Planning.GraphPlan;
using LogHelper;

namespace AIPlanningExample {
    class Program {
        static void Main(string[] args) {
            var factory = new GpActionFactory();
            var initialState = factory.StringToSentence(new() {
                "At(Subject1,mylocation)",
                "-At(Subject1,Supermarket)",
                "-At(Subject1,Work)",
                "-At(Subject1,Home)",
                "-Have(Cake)",
                "Food(Cake)",
                "-Drink(Cake)",
                "Subject(Subject1)"
            });
            var goals = factory.StringToSentence(new() { "Have(Cake)", "At(Subject1,Home)" });

            var work = factory.Create("Work", new() { "At(z, Work)", "Subject(z)" }, new() { "Have(Money)" });
            var buyFood = factory.Create("BuyFood", new() { "At(z, Supermarket)", "Have(Money)", "Food(x)", "Subject(z)" }, new() { "Have(x)", "-Have(Money)" });
            var move = factory.Create("Move", new() { "-At(z, x)", "At(z, y)", "Subject(z)" }, new() { "At(z, x)", "-At(z, y)" });

            var problem = new GpProblem(initialState, goals, new() { move, work, buyFood });
            var solution = problem.Solve();
            Logger.Log(solution.ToString());
        }
    }
}
