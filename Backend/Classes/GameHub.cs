using Backend.Classes.Database;
using Backend.Classes.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace Backend.Classes
{
    public class Game
    {
        public string LobbyId { get; set; }
        public int ScoreToWin { get; set; }
        public List<Player> Players { get; set; } = new List<Player>();
        public List<AnswerCardDTO> AnswersCards { get; set; }
        public Dictionary<string, List<AnswerCard>> PlayerHand { get; set; }
        public string CurrentQuestionPlayer { get; set; }
        public QuestionCard CurrentQuestion { get; set; }
        public bool GameInProgress { get; set; }
    }

    public class GameStateManager
    {
        private readonly ConcurrentDictionary<string, Game> _games = new();
        private readonly LobbyManager _lobbyManager;

        public GameStateManager(LobbyManager lobbyManager)
        {
            _lobbyManager = lobbyManager;
        }

        public Game CreateGame(string lobbyId)
        {
            var lobby = _lobbyManager.GetLobby(lobbyId);
            if (lobby == null)
                throw new Exception("Lobby not found for game creation.");

            var game = new Game
            {
                LobbyId = lobby.LobbyId,
                ScoreToWin = lobby.ScoreToWin,
                Players = lobby.Players,
                PlayerHand = new Dictionary<string, List<AnswerCard>>(),
                GameInProgress = false
            };

            // If you want to track players for each nickname, set them up here:
            foreach (var p in lobby.Players)
            {
                game.PlayerHand[p.Nickname] = new List<AnswerCard>();
            }

            _games[lobbyId] = game;
            return game;
        }

        public Game? GetGame(string lobbyId)
        {
            _games.TryGetValue(lobbyId, out Game state);
            return state;
        }
    }

    [AllowAnonymous]
    public class GameHub : Hub
    {
        private readonly GameStateManager _gameStateManager;
        private readonly CardsDBContext _context;

        public GameHub(GameStateManager gameStateManager, CardsDBContext context)
        {
            _gameStateManager = gameStateManager;
            _context = context;
        }

        public async Task CreateGame(string lobbyId)
        {
            try
            {
                Game game = _gameStateManager.CreateGame(lobbyId);
            }
            catch (Exception ex)
            {
                Console.WriteLine("CreateLobby error: " + ex.Message);
                throw;
            }
        }
    }
}
