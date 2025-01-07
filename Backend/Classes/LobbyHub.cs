﻿using Backend.Classes.Database;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using Backend.Classes.DTO;

namespace Backend.Classes
{
    public class Lobby
    {
        public string LobbyId { get; set; } = Guid.NewGuid().ToString();
        public string HostNickname { get; set; }
        public List<Player> Players { get; set; } = new List<Player>();
        public List<AnswerDeck> SelectedAnswersDecks { get; set; } = new List<AnswerDeck>();
        public List<QuestionDeck> SelectedQuestionsDecks { get; set; } = new List<QuestionDeck>();
        public int ScoreToWin { get; set; } = 0;
        public bool GameStarted { get; set; } = false;
    }

    // In-memory manager
    public class LobbyManager
    {
        private readonly ConcurrentDictionary<string, Lobby> _lobbies = new();

        public Lobby CreateLobby(string hostNickname)
        {
            var lobby = new Lobby
            {
                HostNickname = hostNickname
            };
            // Host automatically joins the lobby
            lobby.Players.Add(new Player
            {
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

        public async Task CreateLobby(string nickname)
        {
            try
            {
                var lobby = _lobbyManager.CreateLobby(nickname);

                await Clients.Caller.SendAsync("LobbyCreated", new CreateLobbyDTO
                {
                    LobbyId = lobby.LobbyId,
                    HostNickname = lobby.HostNickname
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine("CreateLobby error: " + ex.Message);
                throw;
            }
        }

        public async Task JoinLobby(string lobbyId, string nickname)
        {
            var lobby = _lobbyManager.GetLobby(lobbyId);
            if (lobby == null)
                throw new HubException("Lobby not found.");

            if (lobby.Players.Any(p => p.Nickname == nickname))
                throw new HubException("That nickname is already taken in this lobby.");

            await Groups.AddToGroupAsync(Context.ConnectionId, lobbyId);

            var player = new Player
            {
                Nickname = nickname
            };
            lobby.Players.Add(player);


            await Clients.Caller.SendAsync("JoinedLobby", new
            {
                LobbyId = lobby.LobbyId,
                Players = lobby.Players, //list
            });

            await Clients.OthersInGroup(lobbyId).SendAsync("PlayerJoined", player);
        }

        public async Task<LobbyInfoDTO> GetLobbyDetails(string lobbyId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, lobbyId);
            var lobby = _lobbyManager.GetLobby(lobbyId);
            if (lobby == null)
                throw new HubException("Lobby not found.");

            LobbyInfoDTO lobbyInfo = new LobbyInfoDTO()
            {
                LobbyId = lobby.LobbyId,
                HostNickname = lobby.HostNickname,
                Players = lobby.Players
            };

            return lobbyInfo;
        }

        public async Task StartGame(GameInfoDTO gameinfo)
        {
            Console.WriteLine($"Redirecting to: {gameinfo.lobbyID}");
            await Clients.Group(gameinfo.lobbyID).SendAsync("GameplayRedirection", gameinfo.lobbyID);
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