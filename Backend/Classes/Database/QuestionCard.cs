namespace Backend.Classes.Database
{
    public class QuestionCard
    {
        public int Id { get; set; }
        public string Text { get; set; }
        public int Number { get; set; }

        // Foreign key to QuestionDeck
        public int QuestionDeckId { get; set; }
        public QuestionDeck QuestionDeck { get; set; }
    }

}
