using Backend.Classes.Database;
using Backend.Classes.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;


namespace Backend.Classes
{
    public class Game
    {
        public int ScoreToWin { get; set; }
        public List<Player> Players { get; set; } = new List<Player>();
        public int AmountOfPlayers { get; set; }
        public List<AnswerCardDTO> AnswerCards { get; set; }
        public List<QuestionCardDTO> QuestionCards { get; set; }
        public Dictionary<string, List<AnswerCardDTO>> PlayerHand { get; set; }
        public int MaxCardsOnHand = 6;
        public QuestionCardDTO CurrentQuestion { get; set; }
        public string CurrenCardCzar { get; set; }
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

            List<AnswerCardDTO> usableAnswersDeck = new List<AnswerCardDTO>();
            List<QuestionCardDTO> usableQuestionsDeck = new List<QuestionCardDTO>();

            foreach (var deck in lobby.SelectedAnswersDecks)
            {
                List<AnswerCardDTO> cards = db.AnswerCards
                    .Where(x => x.AnswerDeckId == deck.Id)
                    .Select(x => new AnswerCardDTO
                    {
                        Id = x.Id,
                        Text = x.Text
                    }).OrderBy(_ => Guid.NewGuid()).ToList();

                usableAnswersDeck.AddRange(cards);
            }

            foreach (var deck in lobby.SelectedQuestionsDecks)
            {
                List<QuestionCardDTO> cards = db.QuestionCards
                    .Where(x => x.QuestionDeckId == deck.Id)
                    .Select(x => new QuestionCardDTO
                    {
                        Id = x.Id,
                        Text = x.Text,
                        Number = x.Number
                    }).OrderBy(_ => Guid.NewGuid()).ToList();

                usableQuestionsDeck.AddRange(cards);
            }

            Game game = new Game
            {
                ScoreToWin = lobby.ScoreToWin,
                Players = lobby.Players.OrderBy(p => p.Nickname).ToList(),
                AmountOfPlayers = lobby.AmountOfPlayers,
                AnswerCards = usableAnswersDeck,
                QuestionCards = usableQuestionsDeck,
                PlayerHand = new Dictionary<string, List<AnswerCardDTO>>(),
            };

            game.CurrentQuestion = game.QuestionCards.First();
            var random = new Random();
            game.CurrenCardCzar = game.Players[random.Next(game.Players.Count)].Nickname;


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

        public async Task JoinToGameGroup(string lobbyId)
        {
            string tmpGroup = lobbyId + "sub";
            Console.WriteLine(tmpGroup);
            await Groups.AddToGroupAsync(Context.ConnectionId, tmpGroup);
        }

        public async Task CreateGame(string lobbyId)
        {
            Console.WriteLine($"Creating a game: {lobbyId}");
            try
            {
                Game game = _gameManager.CreateGame(lobbyId);
            }
            catch (Exception ex)
            {
                Console.WriteLine("CreateLobby error: " + ex.Message);
                throw;
            }
            Console.WriteLine($"Game created: {lobbyId}");
            string tmpGroup = lobbyId + "sub";
            await Clients.Group(tmpGroup).SendAsync("GameplayRedirection", lobbyId);
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
                game.PlayerHand[nickname] = new List<AnswerCardDTO>();
            }

            List<AnswerCardDTO> currentHand = game.PlayerHand[nickname];
            while (currentHand.Count < game.MaxCardsOnHand && game.AnswerCards.Any())
            {
                AnswerCardDTO nextCard = game.AnswerCards.First();
                game.AnswerCards.RemoveAt(0);

                currentHand.Add(nextCard);
            }
            await Clients.Caller.SendAsync("ReceiveHand", currentHand);

            if (game.AmountOfPlayers == game.Players.Count)
            {
                GetGameInfo(lobbyId);
            }
        }

        public async Task<GameInfoDTO> GetGameInfo(string gameId)
        {
            Game game = _gameManager.GetGame(gameId);
            GameInfoDTO gameInfo = new GameInfoDTO()
            {
                Players = game.Players,
                CardCzar = game.CurrenCardCzar,
                CurrentQuestionCard = game.CurrentQuestion
            };
            return gameInfo;
        }

        public async Task<PlayedCardsDTO> CardsPlayed(string gameId, string nickname, List<int> cards)
        {
            //tmp code
            PlayedCardsDTO dto = new();
            return dto;
        }

        public async Task StartNextRound(string lobbyId)
        {
            var game = _gameManager.GetGame(lobbyId);
            if (game == null)
                throw new HubException("Game not found.");

            // 1) Rotate card czar
            // find the current czar's index
            var players = game.Players;
            int currentCzarIndex = players.FindIndex(p => p.Nickname == game.CurrenCardCzar);
            if (currentCzarIndex == -1)
            {
                // fallback: maybe pick index=0 if not found
                currentCzarIndex = 0;
            }
            // next index
            int nextIndex = (currentCzarIndex + 1) % players.Count;
            game.CurrenCardCzar = players[nextIndex].Nickname;

            // 2) Draw the next question card
            if (game.QuestionCards.Any())
            {
                game.CurrentQuestion = game.QuestionCards.First();
                game.QuestionCards.RemoveAt(0);
            }
            else
            {
                // handle "out of question cards" scenario
            }

            // 3) Broadcast updated info to all players
            await Clients.Group(lobbyId).SendAsync("RoundStarted", new
            {
                CardCzar = game.CurrenCardCzar,
                Question = game.CurrentQuestion
            });
        }

    }
}
