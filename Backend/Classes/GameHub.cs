using Backend.Classes.Database;
using Backend.Classes.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Concurrent;

namespace Backend.Classes
{
	public class ExtendedGameStateDTO
	{
		public QuestionCardDTO CurrentQuestion { get; set; }
		public string CurrentCzar { get; set; }
		public List<Player> Players { get; set; }
		public List<AnswerCardDTO> MyHand { get; set; }
	}

	public class RoundData
	{
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
		public List<AnswerCardDTO> OriginalAnswersDeck { get; set; }
		public List<QuestionCardDTO> OriginalQuestionsDeck { get; set; }
		public Dictionary<string, List<AnswerCardDTO>> PlayerHand { get; set; }
		public int MaxCardsOnHand = 6;
		public QuestionCardDTO CurrentQuestion { get; set; }
		public string CurrenCardCzar { get; set; }
		public RoundData CurrentRound { get; set; } = new RoundData();
		public bool IsFinished { get; set; } = false;
	}


	public class GameManager
	{
		private readonly LobbyManager _lobbyManager;
		private readonly IServiceScopeFactory _scopeFactory;
		private readonly ConcurrentDictionary<string, Game> _games = new();

		public GameManager(LobbyManager lobbyManager, IServiceScopeFactory scopeFactory)
		{
			_lobbyManager = lobbyManager;
			_scopeFactory = scopeFactory;
		}

		public Game CreateGame(string lobbyId)
		{
			var lobby = _lobbyManager.GetLobby(lobbyId);
			if (lobby == null)
				throw new Exception("Lobby not found for game creation.");

			using var scope = _scopeFactory.CreateScope();
			var db = scope.ServiceProvider.GetRequiredService<CardsDBContext>();

			var usableAnswersDeck = new List<AnswerCardDTO>();
			var usableQuestionsDeck = new List<QuestionCardDTO>();

			foreach (var deck in lobby.SelectedAnswersDecks)
			{
				var cards = db.AnswerCards
					.Where(x => x.AnswerDeckId == deck.Id)
					.Select(x => new AnswerCardDTO
					{
						Id = x.Id,
						Text = x.Text
					})
					.OrderBy(_ => Guid.NewGuid())
					.ToList();
				usableAnswersDeck.AddRange(cards);
			}

			foreach (var deck in lobby.SelectedQuestionsDecks)
			{
				var cards = db.QuestionCards
					.Where(x => x.QuestionDeckId == deck.Id)
					.Select(x => new QuestionCardDTO
					{
						Id = x.Id,
						Text = x.Text,
						Number = x.Number
					})
					.OrderBy(_ => Guid.NewGuid())
					.ToList();
				usableQuestionsDeck.AddRange(cards);
			}

			var game = new Game
			{
				ScoreToWin = lobby.ScoreToWin,
				Players = lobby.Players.OrderBy(p => p.Nickname).ToList(),
				AmountOfPlayers = lobby.AmountOfPlayers,

				AnswerCards = usableAnswersDeck,
				QuestionCards = usableQuestionsDeck,

				OriginalAnswersDeck = new List<AnswerCardDTO>(usableAnswersDeck),
				OriginalQuestionsDeck = new List<QuestionCardDTO>(usableQuestionsDeck),

				PlayerHand = new Dictionary<string, List<AnswerCardDTO>>()
			};

			game.CurrentQuestion = game.QuestionCards.First();
			game.QuestionCards.RemoveAt(0);

			var random = new Random();
			game.CurrenCardCzar = game.Players[random.Next(game.Players.Count)].Nickname;

			_games[lobby.LobbyId] = game;
			return game;
		}

		public Game GetGame(string lobbyId)
		{
			_games.TryGetValue(lobbyId, out var state);
			return state;
		}

		public bool RemoveGame(string lobbyId)
		{
			return _games.TryRemove(lobbyId, out _);
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
			try
			{
				var game = _gameManager.CreateGame(lobbyId);
				Console.WriteLine($"Game created: {lobbyId}");
			}
			catch (Exception ex)
			{
				Console.WriteLine("CreateLobby error: " + ex.Message);
				throw;
			}

			var tmpGroup = lobbyId + "sub";
			await Clients.Group(tmpGroup).SendAsync("GameplayRedirection", lobbyId);
		}

		public async Task JoinGame(string lobbyId, string nickname)
		{
			var game = _gameManager.GetGame(lobbyId);
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

			var currentHand = game.PlayerHand[nickname];
			while (currentHand.Count < game.MaxCardsOnHand && game.AnswerCards.Any())
			{
				var nextCard = game.AnswerCards.First();
				game.AnswerCards.RemoveAt(0);
				currentHand.Add(nextCard);
			}

			await Clients.Caller.SendAsync("ReceiveHand", currentHand);

		}

		public async Task<GameInfoDTO> GetGameInfo(string gameId)
		{
			var game = _gameManager.GetGame(gameId);
			if (game == null)
				throw new HubException("Game not found.");

			return new GameInfoDTO
			{
				Players = game.Players,
				CardCzar = game.CurrenCardCzar,
				CurrentQuestionCard = game.CurrentQuestion
			};
		}

		[HubMethodName("GetFullGameState")]
		public async Task<ExtendedGameStateDTO> GetFullGameState(string gameId, string nickname)
		{
			var game = _gameManager.GetGame(gameId);
			if (game == null)
				throw new HubException("Game not found.");

			game.PlayerHand.TryGetValue(nickname, out var myHand);

			var dto = new ExtendedGameStateDTO
			{
				CurrentQuestion = game.CurrentQuestion,
				CurrentCzar = game.CurrenCardCzar,
				Players = game.Players,
				MyHand = myHand ?? new List<AnswerCardDTO>()
			};
			return dto;
		}

		public async Task<PlayedCardsDTO> CardsPlayed(string gameId, string nickname, List<int> cardIds)
		{
			var game = _gameManager.GetGame(gameId);
			if (game == null)
				throw new HubException("Game not found.");

			if (game.CurrenCardCzar == nickname)
				throw new HubException("The question-card player can't play answers.");

			if (cardIds.Count != 1)
				throw new HubException("You must select exactly 1 card at a time.");

			var hand = game.PlayerHand[nickname];
			var playedCardDTOs = new List<AnswerCardDTO>();

			foreach (var cardId in cardIds)
			{
				var cardToRemove = hand.FirstOrDefault(x => x.Id == cardId);
				if (cardToRemove != null)
				{
					hand.Remove(cardToRemove);
					playedCardDTOs.Add(cardToRemove);
				}
			}

			if (!game.CurrentRound.PlayedCardsByPlayer.ContainsKey(nickname))
			{
				game.CurrentRound.PlayedCardsByPlayer[nickname] = new List<AnswerCardDTO>();
			}
			game.CurrentRound.PlayedCardsByPlayer[nickname].AddRange(playedCardDTOs);

			if (!game.CurrentRound.TimesPlayed.ContainsKey(nickname))
				game.CurrentRound.TimesPlayed[nickname] = 0;
			game.CurrentRound.TimesPlayed[nickname] += playedCardDTOs.Count;

			await Clients.Caller.SendAsync("ReceiveHand", hand);

			await CheckIfAllAnswersIn(game, gameId);
			return new PlayedCardsDTO
			{
				Nickname = nickname,
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
					doneCount++;
			}

			if (doneCount == totalPlayers)
			{
				game.CurrentRound.AllAnswersIn = true;
				var allAnswers = new List<PlayedCardsDTO>();

				foreach (var kvp in game.CurrentRound.PlayedCardsByPlayer)
				{
					if (kvp.Key == czar) continue;
					var answerCards = kvp.Value;

					var dto = new PlayedCardsDTO
					{
						Nickname = kvp.Key,
						CardIds = answerCards.Select(c => c.Id).ToList(),
						AnswerCards = answerCards
					};
					allAnswers.Add(dto);
				}

				allAnswers = allAnswers.OrderBy(_ => Guid.NewGuid()).ToList();
				await Clients.Group(gameId).SendAsync("AllAnswersIn", allAnswers);
			}
		}

		public async Task ChooseWinner(string gameId, string czarNickname, string winnerNickname, List<int> winningCards)
		{
			var game = _gameManager.GetGame(gameId);
			if (game == null)
				throw new HubException("Game not found.");

			if (game.CurrenCardCzar != czarNickname)
				throw new HubException("Only the card czar can choose the winner.");

			var winnerPlayer = game.Players.FirstOrDefault(p => p.Nickname == winnerNickname);
			if (winnerPlayer == null)
				throw new HubException("Winner not found in game.");

			winnerPlayer.Score += 1;

			if (game.ScoreToWin > 0 && winnerPlayer.Score >= game.ScoreToWin)
			{
				game.IsFinished = true;

				winnerPlayer.Score = 9999;

				await Clients.Group(gameId).SendAsync("GameOver", new
				{
					Winner = winnerNickname
				});

				_gameManager.RemoveGame(gameId);

				return;
			}

			FillHands(game);

			await StartNextRound(gameId);

			await Clients.Group(gameId).SendAsync("WinnerChosen", new
			{
				Winner = winnerNickname,
				Cards = winningCards
			});
		}

		public async Task StartNextRound(string lobbyId)
		{
			var game = _gameManager.GetGame(lobbyId);
			if (game == null)
				throw new HubException("Game not found.");

			if (game.IsFinished) return;

			var players = game.Players;
			int currentCzarIndex = players.FindIndex(p => p.Nickname == game.CurrenCardCzar);
			if (currentCzarIndex == -1)
				currentCzarIndex = 0;

			int nextIndex = (currentCzarIndex + 1) % players.Count;
			game.CurrenCardCzar = players[nextIndex].Nickname;

			if (!game.QuestionCards.Any())
			{
				ReloadQuestionDeck(game); 
			}

			if (game.QuestionCards.Any())
			{
				game.CurrentQuestion = game.QuestionCards.First();
				game.QuestionCards.RemoveAt(0);
			}

			game.CurrentRound = new RoundData();

			await Clients.Group(lobbyId).SendAsync("RoundStarted");
		}

		private void FillHands(Game game)
		{
			foreach (var player in game.Players)
			{
				var nickname = player.Nickname;
				var hand = game.PlayerHand[nickname];

				if (game.AnswerCards.Count < (game.MaxCardsOnHand - hand.Count))
				{
					ReloadAnswerDeck(game);
				}

				while (hand.Count < game.MaxCardsOnHand && game.AnswerCards.Any())
				{
					var nextCard = game.AnswerCards.First();
					game.AnswerCards.RemoveAt(0);
					hand.Add(nextCard);
				}
			}
		}

		private void ReloadAnswerDeck(Game game)
		{
			game.AnswerCards = game.OriginalAnswersDeck
				.OrderBy(_ => Guid.NewGuid())
				.ToList();
		}

		private void ReloadQuestionDeck(Game game)
		{
			game.QuestionCards = game.OriginalQuestionsDeck
				.OrderBy(_ => Guid.NewGuid())
				.ToList();
		}
	}
}
