using Backend.Classes.Database;

namespace Backend.Classes.DTO
{
    public class GameInfoDTO
    {
        public string lobbyID {  get; set; }
        public int ScoreToWin { get; set; }
        public List<AnswerDeck> ChosenAnswersDecks {  get; set; }
        public List<QuestionDeck> ChosenQuestionsDecks {  get; set; }
    }
}
