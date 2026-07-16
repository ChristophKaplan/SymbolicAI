using System.Collections.Generic;
using System.Linq;
using FirstOrderLogic;

namespace AIPlanning.Planning.GraphPlan {
    public class GpActionFactory {
        private readonly FirstOrderLogic.FirstOrderLogic _logic;

        // Pass a shared logic so all parsing goes through one parse table and sentences
        // stay structurally comparable.
        public GpActionFactory(FirstOrderLogic.FirstOrderLogic? logic = null) {
            _logic = logic ?? new FirstOrderLogic.FirstOrderLogic();
        }

        public GpAction Create(string name, List<string> preconditions, List<string> effects) {
            return new GpAction(name, ParseSentences(preconditions), ParseSentences(effects));
        }

        public List<ISentence> ParseSentences(List<string> strings) {
            return strings.Select(s => (ISentence)_logic.Parse(s)).ToList();
        }
    }
}
