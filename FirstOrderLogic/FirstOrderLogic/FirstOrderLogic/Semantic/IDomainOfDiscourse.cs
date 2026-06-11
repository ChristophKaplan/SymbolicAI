using System.Collections.Generic;

namespace FirstOrderLogic {
    public interface IDomainOfDiscourse {
        public List<IElementOfDiscourse> Elements { get; }
    }
}