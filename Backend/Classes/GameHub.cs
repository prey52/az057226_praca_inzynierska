using Backend.Classes.Database;
using Backend.Classes.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace Backend.Classes
{
    public class Game
    {
        public int ScoreToWin { get; set; }
        public List<Player> Players { get; set; } = new List<Player>();
        public int AmountOfPlayers { get; set; }
        public List<CardDTO> AnswerCards { get; set; }
        public List<CardDTO> QuestionCards { get; set; }
        public Dictionary<string, List<CardDTO>> PlayerHand { get; set; }
        public int MaxCardsOnHand = 6;
        public string CurrentQuestionPlayer { get; set; }
        public CardDTO CurrentQuestion { get; set; }
    }

    public class GameManager
    {
        private readonly LobbyManager _lobbyManager;
        private readonly IServiceScopeFactory _scopeFactory;

        private readonly ConcurrentDictionary<string, Game> _games = new();

        public GameManager(LobbyManager lobbyManager, IServiceScopeFactory scopeFactory)
        {
            _lobbyManager = lobbyManager;
            _scopeFactory = scopeFactory;  // Not the scoped DB context
        }


        public Game CreateGame(string lobbyId)
        {
            Lobby lobby = _lobbyManager.GetLobby(lobbyId);
            if (lobby == null)
                throw new Exception("Lobby not found for game creation.");

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CardsDBContext>();



            List<CardDTO> usableAnswersDeck = new List<CardDTO>();
            List<CardDTO> usableQuestionsDeck = new List<CardDTO>();

            foreach (var deck in lobby.SelectedAnswersDecks)
            {
                List<CardDTO> cards = db.AnswerCards
                    .Where(x => x.AnswerDeckId == deck.Id)
                    .Select(x => new CardDTO
                    {
                        Id = x.Id,
                        Text = x.Text
                    }).OrderBy(_ => Guid.NewGuid()).ToList();

                usableAnswersDeck.AddRange(cards);
            }

            foreach (var deck in lobby.SelectedQuestionsDecks)
            {
                List<CardDTO> cards = db.QuestionCards
                    .Where(x => x.QuestionDeckId == deck.Id)
                    .Select(x => new CardDTO
                    {
                        Id = x.Id,
                        Text = x.Text
                    }).OrderBy(_ => Guid.NewGuid()).ToList();

                usableQuestionsDeck.AddRange(cards);
            }

            Game game = new Game
            {
                ScoreToWin = lobby.ScoreToWin,
                Players = lobby.Players,
                AmountOfPlayers = lobby.AmountOfPlayers,
                AnswerCards = usableAnswersDeck,
                QuestionCards = usableQuestionsDeck,
                PlayerHand = new Dictionary<string, List<CardDTO>>(),
            };

            _games[lobby.LobbyId] = game;
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
        private readonly GameManager _gameManager;

        public GameHub(GameManager gameStateManager)
        {
            _gameManager = gameStateManager;
        }

        public async Task CreateGame(string lobbyId)
        {
            try
            {
                Game game = _gameManager.CreateGame(lobbyId);
            }
            catch (Exception ex)
            {
                Console.WriteLine("CreateLobby error: " + ex.Message);
                throw;
            }
        }

        public async Task JoinGame(string lobbyId, string nickname)
        {
            Game game = _gameManager.GetGame(lobbyId);
            if (game == null)
                throw new HubException("Game not found.");

            await Groups.AddToGroupAsync(Context.ConnectionId, lobbyId);

            if (!game.Players.Any(p => p.Nickname == nickname))
            {
                game.Players.Add(new Player { Nickname = nickname });
            }

            if (!game.PlayerHand.ContainsKey(nickname))
            {
                game.PlayerHand[nickname] = new List<CardDTO>();
            }


            List<CardDTO> currentHand = game.PlayerHand[nickname];
            while (currentHand.Count < game.MaxCardsOnHand && game.AnswerCards.Any())
            {
                CardDTO nextCard = game.AnswerCards.First();
                game.AnswerCards.RemoveAt(0);

                currentHand.Add(nextCard);
            }
            await Clients.Caller.SendAsync("ReceiveHand", currentHand);

            if (game.AmountOfPlayers == game.Players.Count)
            {
                await Clients.Group(lobbyId).SendAsync("StartGame", "ok");
            }
        }
    }
}
