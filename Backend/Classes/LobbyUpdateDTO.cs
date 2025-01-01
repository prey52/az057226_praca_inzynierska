namespace Backend.Classes
{
    public class LobbyUpdateDTO
    {
        public string LobbyId { get; set; }
        public List<string> SelectedDecks { get; set; }
        public int ScoreToWin { get; set; }
    }
}
