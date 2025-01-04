namespace AZ_Inz.Classes
{
    public class JoinedLobbyResponse
    {
        public string LobbyId { get; set; }
        public List<Player> Players { get; set; } = new();
        public string HostId { get; set; }
    }

}
