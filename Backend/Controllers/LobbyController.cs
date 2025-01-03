using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using Backend.Classes;

namespace Backend.Controllers
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

        [AllowAnonymous]
        [HttpPost("create-lobby")]
        public IActionResult CreateLobby([FromBody] CreateLobbyRequest request)
        {
            // Check if the user is logged in
            var userId = this.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

            // Determine the host's nickname
            string hostNickname;
            if (string.IsNullOrEmpty(userId))
            {
                if (string.IsNullOrEmpty(request.Nickname))
                {
                    return BadRequest("Nickname is required for unlogged users.");
                }
                userId = Guid.NewGuid().ToString(); // Generate a temporary user ID for unlogged users
                hostNickname = request.Nickname;
            }
            else
            {
                hostNickname = this.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value ?? "Host";
            }

            var lobby = _lobbyManager.CreateLobby(userId);
            lobby.HostNickname = hostNickname;

            lobby.Players.Add(new Player
            {
                PlayerId = userId,
                Nickname = hostNickname
            });

            return Ok(new
            {
                lobby.LobbyId,
                HostId = lobby.HostId,
                HostNickname = lobby.HostNickname
            });

        }

        [AllowAnonymous]
        [HttpPost("join-lobby")]
        public async Task<IActionResult> JoinLobby([FromBody] JoinLobbyRequest request)
        {
            var lobby = _lobbyManager.GetLobby(request.LobbyId);
            if (lobby == null) return NotFound("Lobby not found.");
            if (lobby.Players.Any(p => p.PlayerId == request.PlayerId)) return BadRequest("User already in the lobby.");
            if (request.PlayerId == null && request.Nickname == null) return BadRequest("Nickname or loggen in user ID requested");

            var player = new Player
            {
                PlayerId = request.PlayerId,
                Nickname = string.IsNullOrEmpty(request.PlayerId) ? request.Nickname : this.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value
            };
            lobby.Players.Add(player);

            await _hubContext.Clients.Group(request.LobbyId).SendAsync("PlayerJoined", player);

            return Ok(lobby);
        }

        [AllowAnonymous]
        [HttpGet("{lobbyId}")]
        public ActionResult<Lobby> GetLobby(string lobbyId)
        {
            var lobby = _lobbyManager.GetLobby(lobbyId);
            if (lobby == null)
            {
                return NotFound("Lobby not found.");
            }

            return Ok(lobby);
        }

    }
}