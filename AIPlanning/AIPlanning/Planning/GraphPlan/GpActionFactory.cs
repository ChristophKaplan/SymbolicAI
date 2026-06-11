using System.Collections.Generic;
using System.Linq;
using FirstOrderLogic;

namespace AIPlanning.Planning.GraphPlan {
    public class GpActionFactory {
        private readonly FirstOrderLogic.FirstOrderLogic _logic;

        /// <summary>
        /// Creates a factory bound to <paramref name="logic"/>. Pass a shared instance so all
        /// parsing goes through one logic (one parse table, structurally comparable sentences);
        /// if none is supplied a private instance is created.
        /// </summary>
        public GpActionFactory(FirstOrderLogic.FirstOrderLogic? logic = null) {
            _logic = logic ?? new FirstOrderLogic.FirstOrderLogic();
        }

        public GpAction Create(string name, List<string> preconditions, List<string> effects) {
            return new GpAction(name,
                preconditions.Select(p => (ISentence)_logic.TryParse(p)).ToList(),
                effects.Select(e => (ISentence)_logic.TryParse(e)).ToList());
        }

        public List<ISentence> StringToSentence(List<string> strings) {
            return strings.Select(s => (ISentence)_logic.TryParse(s)).ToList();
        }
    }
}
