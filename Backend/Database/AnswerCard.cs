namespace Backend.Database
{
    public class AnswerCard
    {
        public int Id { get; set; }
        public string Text { get; set; }

        // Foreign key to AnswerDeck
        public int AnswerDeckId { get; set; }
        public AnswerDeck AnswerDeck { get; set; }
    }

}
