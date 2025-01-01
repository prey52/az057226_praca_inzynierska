namespace AZ_Inz.Classes
{
    public class LobbyUpdateDto
    {
        public string LobbyId { get; set; }
        public List<string> SelectedDecks { get; set; } = new();
        public int ScoreToWin { get; set; }
    }
}
