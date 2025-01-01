namespace AZ_Inz.Classes
{
    public class Lobby
    {
        public string LobbyId { get; set; }
        public string HostId { get; set; }
        public List<Player> Players { get; set; } = new();
    }
}
