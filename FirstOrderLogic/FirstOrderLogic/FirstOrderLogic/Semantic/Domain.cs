using System.Collections.Generic;

namespace FirstOrderLogic {
    public class Domain : IDomainOfDiscourse {
        public List<IElementOfDiscourse> Elements { get; } = new();
    
        public Domain(params IElementOfDiscourse[] elements) {
            Elements.AddRange(elements);
        }
    }
}