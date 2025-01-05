namespace AZ_Inz.Classes
{
    public class LobbyInfoDTO
    {
        public string LobbyId { get; set; }
        public string HostNickname { get; set; }
        public List<Player> Players { get; set; } = new();
    }
}
