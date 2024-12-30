using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Security.Claims;

namespace YourNamespace.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LobbyController : ControllerBase
    {
        private readonly LobbyManager _lobbyManager;
        private readonly IHubContext<LobbyHub> _hubContext;

        public LobbyController(LobbyManager lobbyManager, IHubContext<LobbyHub> hubContext)
        {
            _lobbyManager = lobbyManager;
            _hubContext = hubContext;
        }

        [HttpPost("create-lobby")]
        [Authorize]
        public IActionResult CreateLobby()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier); // Get the user ID from token
            var lobby = _lobbyManager.CreateLobby(userId);

            return Ok(new { lobby.LobbyId });
        }

        [HttpPost("update-lobby")]
        [Authorize]
        public async Task<IActionResult> UpdateLobby([FromBody] LobbyUpdateDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var lobby = _lobbyManager.GetLobby(dto.LobbyId);

            if (lobby == null) return NotFound("Lobby not found.");
            if (lobby.HostId != userId) return Unauthorized("Only the host can update the lobby.");

            lobby.SelectedDecks = dto.SelectedDecks;
            lobby.ScoreToWin = dto.ScoreToWin;

            // Notify players of the update
            await _hubContext.Clients.Group(dto.LobbyId).SendAsync("LobbyUpdated", lobby);

            return Ok(lobby);
        }

        [HttpPost("join-lobby")]
        [Authorize]
        public async Task<IActionResult> JoinLobby([FromBody] string lobbyId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var lobby = _lobbyManager.GetLobby(lobbyId);

            if (lobby == null) return NotFound("Lobby not found.");
            if (lobby.PlayerIds.Contains(userId)) return BadRequest("User already in the lobby.");

            lobby.PlayerIds.Add(userId);

            // Add user to SignalR group
            await _hubContext.Groups.AddToGroupAsync(HttpContext.Connection.Id, lobbyId);
            await _hubContext.Clients.Group(lobbyId).SendAsync("PlayerJoined", userId);

            return Ok(lobby);
        }
    }

    // Lobby Model
    public class Lobby
    {
        public string LobbyId { get; set; } = Guid.NewGuid().ToString();
        public string HostId { get; set; }
        public List<string> PlayerIds { get; set; } = new List<string>();
        public List<string> SelectedDecks { get; set; } = new List<string>();
        public int ScoreToWin { get; set; }
        public bool GameStarted { get; set; } = false;
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

    // Lobby DTO
    public class LobbyUpdateDto
    {
        public string LobbyId { get; set; }
        public List<string> SelectedDecks { get; set; }
        public int ScoreToWin { get; set; }
    }

    // SignalR Hub for Lobby Events
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

            lobby.PlayerIds.Add(Context.UserIdentifier);
            await Groups.AddToGroupAsync(Context.ConnectionId, lobbyId);
            await Clients.Group(lobbyId).SendAsync("PlayerJoined", Context.UserIdentifier);
        }

        public async Task UpdateLobby(string lobbyId)
        {
            var lobby = _lobbyManager.GetLobby(lobbyId);
            if (lobby == null) throw new HubException("Lobby not found.");

            await Clients.Group(lobbyId).SendAsync("LobbyUpdated", lobby);
        }
    }
}
