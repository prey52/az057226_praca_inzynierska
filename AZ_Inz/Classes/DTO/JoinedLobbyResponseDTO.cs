namespace AZ_Inz.Classes.DTO
{
    public class JoinedLobbyResponseDTO
    {
        public string LobbyId { get; set; }
        public List<Player> Players { get; set; } = new();
    }
}
