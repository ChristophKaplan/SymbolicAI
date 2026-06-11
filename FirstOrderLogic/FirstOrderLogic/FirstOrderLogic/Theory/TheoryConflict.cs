namespace FirstOrderLogic
{
    // A claim held by one theory and its complement held (or entailed) by another.
    // Detection only — use KernelSets to explain why either side holds its sentence.
    public readonly struct TheoryConflict
    {
        public readonly ISentence Claim;
        public readonly ISentence Counter;

        public TheoryConflict(ISentence claim, ISentence counter)
        {
            Claim   = claim;
            Counter = counter;
        }
    }
}
