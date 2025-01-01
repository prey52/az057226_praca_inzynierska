using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace Backend.Classes
{
    public class Lobby
    {
        public string LobbyId { get; set; } = Guid.NewGuid().ToString();
        public string HostId { get; set; }
        public string HostNickname { get; set; }
        public List<Player> Players { get; set; } = new List<Player>();
        public List<string> SelectedDecks { get; set; } = new List<string>();
        public int ScoreToWin { get; set; } = 0;
        public bool GameStarted { get; set; } = false;

        // Optional: Helper for player IDs
        public List<string> PlayerIds => Players.Select(p => p.PlayerId).ToList();
    }

    // Lobby Manager (In-Memory)
    public class LobbyManager
    {
        private readonly ConcurrentDictionary<string, Lobby> _lobbies = new();

        public Lobby CreateLobby(string hostId)
        {
            var lobby = new Lobby
            {
                HostId = hostId
            };

            _lobbies[lobby.LobbyId] = lobby;
            return lobby;
        }

        public Lobby? GetLobby(string lobbyId)
        {
            _lobbies.TryGetValue(lobbyId, out var lobby);
            return lobby;
        }

        public bool RemoveLobby(string lobbyId)
        {
            return _lobbies.TryRemove(lobbyId, out _);
        }

        public List<Lobby> GetAllLobbies()
        {
            return _lobbies.Values.ToList();
        }
    }

    public class LobbyHub : Hub
    {
        private readonly LobbyManager _lobbyManager;

        public LobbyHub(LobbyManager lobbyManager)
        {
            _lobbyManager = lobbyManager;
        }

        public async Task JoinLobby(string lobbyId)
        {
            var lobby = _lobbyManager.GetLobby(lobbyId);
            if (lobby == null) throw new HubException("Lobby not found.");

            var userId = Context.UserIdentifier;
            if (string.IsNullOrEmpty(userId)) throw new HubException("User must be authenticated.");

            if (lobby.PlayerIds.Contains(userId)) throw new HubException("User already in the lobby.");

            // Add user to SignalR group
            await Groups.AddToGroupAsync(Context.ConnectionId, lobbyId);
            await Clients.Group(lobbyId).SendAsync("PlayerJoined", new { UserId = userId });
        }

        public async Task UpdateLobby(string lobbyId)
        {
            var lobby = _lobbyManager.GetLobby(lobbyId);
            if (lobby == null) throw new HubException("Lobby not found.");

            await Clients.Group(lobbyId).SendAsync("LobbyUpdated", lobby);
        }
    }
}
