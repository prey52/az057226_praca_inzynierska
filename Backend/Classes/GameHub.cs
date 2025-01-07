using Backend.Classes.Database;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace Backend.Classes
{
    public class GameState
    {
        public string LobbyId { get; set; }
        public bool GameInProgress { get; set; }
        public Dictionary<string, List<AnswerCard>> PlayerHands { get; set; }
        public string CurrentQuestionPlayer { get; set; }
        public QuestionCard CurrentQuestion { get; set; }
    }

    public class GameStateManager
    {
        // In-memory or persistent
        private readonly ConcurrentDictionary<string, GameState> _gameStates = new();

        public GameState CreateOrGetGame(string lobbyId)
        {
            return _gameStates.GetOrAdd(lobbyId, id => new GameState
            {
                LobbyId = lobbyId,
                GameInProgress = false,
                PlayerHands = new Dictionary<string, List<AnswerCard>>()
            });
        }

        public GameState? GetGame(string lobbyId)
        {
            _gameStates.TryGetValue(lobbyId, out var state);
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

        public async Task JoinGame(string lobbyId, string nickname)
        {
            // Ensure the game state exists
            var game = _gameStateManager.GetGame(lobbyId);
            if (game == null)
                throw new HubException("Game not found.");

            // Add this user to the game group
            await Groups.AddToGroupAsync(Context.ConnectionId, lobbyId);

            // If user not in dictionary, initialize their hand
            if (!game.PlayerHands.ContainsKey(nickname))
            {
                game.PlayerHands[nickname] = new List<AnswerCard>();
            }

            // Return to caller: your current hand, game state, etc.
            await Clients.Caller.SendAsync("GameJoined", new
            {
                LobbyId = lobbyId,
                YourHand = game.PlayerHands[nickname],
                CurrentQuestion = game.CurrentQuestion
                // ...
            });

            // Optionally notify others that "X joined the game"
            await Clients.OthersInGroup(lobbyId).SendAsync("PlayerJoinedGame", nickname);
        }

        // Called by the host after they load the game page with chosen deck IDs
        /*public async Task StartGame(string lobbyId, int scoreToWin, List<int> answerDeckIds, List<int> questionDeckIds)
        {
            var game = _gameStateManager.CreateOrGetGame(lobbyId);
            if (game.GameInProgress)
                throw new HubException("Game already started.");

            // Retrieve decks from DB
            var answerDecks = _context.AnswerDecks
                .Where(d => answerDeckIds.Contains(d.Id))
                .Include(d => d.AnswerCards) // be sure to load the cards
                .ToList();

            var questionDecks = _context.QuestionDecks
                .Where(d => questionDeckIds.Contains(d.Id))
                .Include(d => d.QuestionCards)
                .ToList();

            // Combine all answer cards
            List<AnswerCard> allAnswerCards = answerDecks
                .SelectMany(d => d.AnswerCards)
                .ToList();

            // Combine all question cards
            List<QuestionCard> allQuestionCards = questionDecks
                .SelectMany(d => d.QuestionCards)
                .ToList();

            // Shuffle them
            allAnswerCards = Shuffle(allAnswerCards);
            allQuestionCards = Shuffle(allQuestionCards);

            // Deal 5 cards to each known player
            foreach (var kvp in game.PlayerHands)
            {
                kvp.Value.Clear(); // empty old hand
                kvp.Value.AddRange(allAnswerCards.Take(5));
                allAnswerCards.RemoveRange(0, 5);
            }

            // Pick first question player
            var firstPlayer = game.PlayerHands.Keys.FirstOrDefault();
            game.CurrentQuestionPlayer = firstPlayer;
            game.CurrentQuestion = allQuestionCards.First();
            allQuestionCards.RemoveAt(0);

            // Mark game as started
            game.GameInProgress = true;

            // Broadcast new game state
            await Clients.Group(lobbyId).SendAsync("GameStarted", new
            {
                ScoreToWin = scoreToWin,
                CurrentQuestionPlayer = game.CurrentQuestionPlayer,
                CurrentQuestion = game.CurrentQuestion
                // ...
            });
        }*/

        public async Task PlayCards(string lobbyId, string nickname, List<int> cardIds)
        {
            // The user picks `cardIds` from their hand
            var game = _gameStateManager.GetGame(lobbyId);
            if (game == null) throw new HubException("Game not found.");

            var hand = game.PlayerHands[nickname];
            // Remove from user’s hand, store in round data, etc.
            // Once everyone has played, the question player picks a winner, etc.

            // Then broadcast partial state or wait until all have played
        }

        // Helper function to shuffle
        private List<T> Shuffle<T>(List<T> list)
        {
            // basic in-place shuffle
            var rng = new Random();
            for (int i = list.Count - 1; i > 0; i--)
            {
                int swapIndex = rng.Next(i + 1);
                (list[i], list[swapIndex]) = (list[swapIndex], list[i]);
            }
            return list;
        }
    }
}
