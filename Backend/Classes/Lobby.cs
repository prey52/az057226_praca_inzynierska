using Backend.Classes.Database;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using Backend.Classes.DTO;

namespace Backend.Classes
{
    public class Lobby
    {
        public string LobbyId { get; set; } = Guid.NewGuid().ToString();
        public string HostId { get; set; }
        public string HostNickname { get; set; }
        public List<Player> Players { get; set; } = new List<Player>();
        public List<AnswerDeck> SelectedAnswersDecks { get; set; } = new List<AnswerDeck>();
        public List<QuestionDeck> SelectedQuestionsDecks { get; set; } = new List<QuestionDeck>();
        public int ScoreToWin { get; set; } = 0;
        public bool GameStarted { get; set; } = false;
        public List<string> PlayerIds => Players.Select(p => p.PlayerId).ToList();
    }

    // In-memory manager
    public class LobbyManager
    {
        private readonly ConcurrentDictionary<string, Lobby> _lobbies = new();

        public Lobby CreateLobby(string hostId, string hostNickname)
        {
            var lobby = new Lobby
            {
                HostId = hostId,
                HostNickname = hostNickname
            };
            // Host automatically joins the lobby
            lobby.Players.Add(new Player
            {
                PlayerId = hostId,
                Nickname = hostNickname
            });

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

        public List<Lobby> GetAllLobbies() => _lobbies.Values.ToList();
    }

    [AllowAnonymous]
    public class LobbyHub : Hub
    {
        private readonly LobbyManager _lobbyManager;
        private readonly CardsDBContext _context;

        public LobbyHub(LobbyManager lobbyManager, CardsDBContext context)
        {
            _lobbyManager = lobbyManager;
            _context = context;
        }

        // Create a new lobby with the given nickname (for anonymous or “guest” user)
        public async Task CreateLobby(string nickname)
        {
            try
            {
                var userId = Guid.NewGuid().ToString();
                var lobby = _lobbyManager.CreateLobby(userId, nickname);

                // Host joins the SignalR group
                await Groups.AddToGroupAsync(Context.ConnectionId, lobby.LobbyId);

                // Return to the caller
                await Clients.Caller.SendAsync("LobbyCreated", new CreateLobbyDTO
                {
                    LobbyId = lobby.LobbyId,
                    HostId = lobby.HostId,
                    HostNickname = lobby.HostNickname
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine("CreateLobby error: " + ex.Message);
                throw; // rethrow or handle
            }
            
        }

        // Join an existing lobby with a nickname
        public async Task JoinLobby(string lobbyId, string nickname, string userId)
        {
            Console.WriteLine("JoinLobby invoked with userId = " + userId);

            var lobby = _lobbyManager.GetLobby(lobbyId);
            if (lobby == null)
                throw new HubException("Lobby not found.");

            // If they've already joined, skip
            if (lobby.PlayerIds.Contains(userId))
                throw new HubException("User already in the lobby.");

            // Add user to SignalR group for real-time updates
            await Groups.AddToGroupAsync(Context.ConnectionId, lobbyId);

            var player = new Player
            {
                PlayerId = userId,
                Nickname = nickname
            };

            lobby.Players.Add(player);

            // Return the updated lobby to the caller
            await Clients.Caller.SendAsync("JoinedLobby", new
            {
                LobbyId = lobby.LobbyId,
                Players = lobby.Players,
                HostId = lobby.HostId
            });

            // Notify others in the lobby about the new player
            await Clients.OthersInGroup(lobbyId).SendAsync("PlayerJoined", player);
        }
    
        public async Task<Lobby> GetLobbyDetails(string lobbyId)
        {
            var lobby = _lobbyManager.GetLobby(lobbyId);
            if (lobby == null)
                throw new HubException("Lobby not found.");

            return lobby;
        }

        public async Task SetLobbyOptions(string lobbyId, int scoreToWin, List<int> answerDeckIds, List<int> questionDeckIds)
        {
            var lobby = _lobbyManager.GetLobby(lobbyId);
            if (lobby == null)
                throw new HubException("Lobby not found.");

            // Check if caller is host
            var userId = Context.UserIdentifier;
            if (lobby.HostId != userId)
                throw new HubException("Only the host can set lobby options.");

            // Update score
            lobby.ScoreToWin = scoreToWin;

            // Clear old decks
            lobby.SelectedAnswersDecks.Clear();
            lobby.SelectedQuestionsDecks.Clear();

            // Query DB for these deck IDs
            var answerDecks = _context.AnswerDecks
                .Where(ad => answerDeckIds.Contains(ad.Id))
                .ToList();

            var questionDecks = _context.QuestionDecks
                .Where(qd => questionDeckIds.Contains(qd.Id))
                .ToList();

            // Add them to the lobby
            lobby.SelectedAnswersDecks.AddRange(answerDecks);
            lobby.SelectedQuestionsDecks.AddRange(questionDecks);

            // Optionally broadcast to others
            await Clients.Group(lobbyId).SendAsync("LobbyOptionsUpdated", new
            {
                ScoreToWin = lobby.ScoreToWin,
                AnswerDecks = lobby.SelectedAnswersDecks.Select(d => new { d.Id, d.Name }),
                QuestionDecks = lobby.SelectedQuestionsDecks.Select(d => new { d.Id, d.Name })
            });
        }


        public async Task LeaveLobby(string lobbyId)
        {
            var lobby = _lobbyManager.GetLobby(lobbyId);
            if (lobby == null) return;

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, lobbyId);
            await Clients.Group(lobbyId).SendAsync("PlayerLeft", $"Connection {Context.ConnectionId} left.");
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await base.OnDisconnectedAsync(exception);
        }
    }
}
