namespace Backend.Classes.DTO
{
    public class GameInfoDTO
    {
        public string lobbyID {  get; set; }
        public int ScoreToWin { get; set; }
        public List<int> ChosenAnswersDecks {  get; set; }
        public List<int> ChosenQuestionsDecks {  get; set; }
    }
}
