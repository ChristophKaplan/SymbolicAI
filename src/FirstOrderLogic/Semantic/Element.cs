namespace FirstOrderLogic {
    public class Element : IElementOfDiscourse {
        public int Id { get; }

        public Element(int id) {
            Id = id;
        }

        public override bool Equals(object? obj) {
            return obj is Element element && Id == element.Id;
        }

        public override int GetHashCode() {
            return Id;
        }
    }
}
