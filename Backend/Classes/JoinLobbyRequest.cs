namespace Backend.Classes
{
    public class JoinLobbyRequest
    {
        public string LobbyId { get; set; }
        public string? PlayerId { get; set; }
        public string? Nickname { get; set; }
    }
}
