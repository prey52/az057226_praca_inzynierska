﻿using Backend.Classes;

namespace Backend.Classes.DTO
{
    public class LobbyInfoDTO
    {
        public string LobbyId { get; set; }
        public string HostNickname { get; set; }
        public List<Player> Players { get; set; } = new();
    }
}