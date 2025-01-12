namespace Backend.Classes.DTO
{
    public class GameInfoDTO
    {
        public List<Player> Players { get; set; }
        public string CardCzar { get; set; }
        public QuestionCardDTO CurrentQuestionCard { get; set; }
    }
}
