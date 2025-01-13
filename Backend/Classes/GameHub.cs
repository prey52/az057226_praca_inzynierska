using Backend.Classes.Database;
using Backend.Classes.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;


namespace Backend.Classes
{
    public class RoundData
    {
        // Now we store a List<AnswerCardDTO> instead of List<int>,
        // so we have direct access to card Text, etc.
        public Dictionary<string, List<AnswerCardDTO>> PlayedCardsByPlayer { get; set; } = new();

        public Dictionary<string, int> TimesPlayed { get; set; } = new();

        public bool AllAnswersIn { get; set; } = false;
    }


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

        // Store current round data
        public RoundData CurrentRound { get; set; } = new RoundData();
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

        public async Task<PlayedCardsDTO> CardsPlayed(string gameId, string nickname, List<int> cardIds)
        {
            var game = _gameManager.GetGame(gameId);
            if (game == null)
                throw new HubException("Game not found.");

            // Forbid the czar
            if (game.CurrenCardCzar == nickname)
                throw new HubException("The question-card player can't play answers.");

            // Single-card approach
            if (cardIds.Count != 1)
                throw new HubException("You must select exactly 1 card at a time.");

            var hand = game.PlayerHand[nickname];

            // We'll gather the actual card objects here
            var playedCardDTOs = new List<AnswerCardDTO>();

            foreach (var cardId in cardIds)
            {
                var cardToRemove = hand.FirstOrDefault(x => x.Id == cardId);
                if (cardToRemove != null)
                {
                    hand.Remove(cardToRemove);
                    // store in a local list so we can put it in RoundData
                    playedCardDTOs.Add(cardToRemove);
                }
            }

            // Now place them in RoundData
            if (!game.CurrentRound.PlayedCardsByPlayer.ContainsKey(nickname))
            {
                game.CurrentRound.PlayedCardsByPlayer[nickname] = new List<AnswerCardDTO>();
            }
            // Add the actual card objects
            game.CurrentRound.PlayedCardsByPlayer[nickname].AddRange(playedCardDTOs);

            // Update times played
            if (!game.CurrentRound.TimesPlayed.ContainsKey(nickname))
                game.CurrentRound.TimesPlayed[nickname] = 0;
            game.CurrentRound.TimesPlayed[nickname] += playedCardDTOs.Count; // which is 1 in your scenario

            // Return or broadcast the updated hand
            await Clients.Caller.SendAsync("ReceiveHand", hand);

            // Check if everyone is done
            await CheckIfAllAnswersIn(game, gameId);

            return new PlayedCardsDTO
            {
                Nickname = nickname,
                // We can return the IDs or the text, up to you. 
                // But let's keep the same shape: card IDs
                CardIds = playedCardDTOs.Select(c => c.Id).ToList()
            };
        }



        private async Task CheckIfAllAnswersIn(Game game, string gameId)
        {
            if (game.CurrentRound.AllAnswersIn) return;

            var questionNumber = game.CurrentQuestion.Number;
            var czar = game.CurrenCardCzar;

            int totalPlayers = game.Players.Count(p => p.Nickname != czar);
            int doneCount = 0;

            foreach (var p in game.Players)
            {
                if (p.Nickname == czar) continue;

                game.CurrentRound.TimesPlayed.TryGetValue(p.Nickname, out int times);
                if (times >= questionNumber)
                {
                    doneCount++;
                }
            }

            if (doneCount == totalPlayers)
            {
                // Everyone answered
                game.CurrentRound.AllAnswersIn = true;

                // We'll build a list of PlayedCardsDTO, but with the full card data
                var allAnswers = new List<PlayedCardsDTO>();

                foreach (var kvp in game.CurrentRound.PlayedCardsByPlayer)
                {
                    if (kvp.Key == czar) continue;

                    // We'll store the real card objects
                    var answerCards = kvp.Value;

                    // Construct a new PlayedCardsDTO that has Nickname + the list of card IDs
                    // If you want to pass the text too, you could define a new property in PlayedCardsDTO
                    // or create another DTO that has card text. 
                    // Let's do the simplest approach: create a new property "PlayedCards" that is a List<AnswerCardDTO>.

                    var dto = new PlayedCardsDTO
                    {
                        Nickname = kvp.Key,
                        // The original shape had CardIds, but let's also add an AnswerCards property
                        CardIds = answerCards.Select(c => c.Id).ToList(),
                        AnswerCards = answerCards // <--- new property (we'll define it below)
                    };
                    allAnswers.Add(dto);
                }

                // randomize order
                allAnswers = allAnswers.OrderBy(_ => Guid.NewGuid()).ToList();

                // broadcast "AllAnswersIn" with the full set
                await Clients.Group(gameId).SendAsync("AllAnswersIn", allAnswers);
            }
        }



        public async Task ChooseWinner(string gameId, string czarNickname, string winnerNickname, List<int> winningCards)
        {
            var game = _gameManager.GetGame(gameId);
            if (game == null)
                throw new HubException("Game not found.");

            // Only the czar can pick
            if (game.CurrenCardCzar != czarNickname)
                throw new HubException("Only the card czar can choose the winner.");

            // 1) Award a point to the winner
            var winnerPlayer = game.Players.FirstOrDefault(p => p.Nickname == winnerNickname);
            if (winnerPlayer == null)
                throw new HubException("Winner not found in game.");

            winnerPlayer.Score += 1;

            // 2) If you want to fill each player's hand back to MaxCardsOnHand, do that
            // E.g. fill everyone except the czar, or everyone, depending on your rules:
            FillHands(game);

            // 3) Start next round => rotate czar, pick new question card
            // e.g. call your StartNextRound method
            await StartNextRound(gameId);

            // 4) You might want to broadcast an event that says who won
            await Clients.Group(gameId).SendAsync("WinnerChosen", new
            {
                Winner = winnerNickname,
                Cards = winningCards
            });
        }

        private void FillHands(Game game)
        {
            foreach (var player in game.Players)
            {
                var nickname = player.Nickname;
                var hand = game.PlayerHand[nickname];
                while (hand.Count < game.MaxCardsOnHand && game.AnswerCards.Any())
                {
                    var nextCard = game.AnswerCards.First();
                    game.AnswerCards.RemoveAt(0);
                    hand.Add(nextCard);
                }
            }
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
