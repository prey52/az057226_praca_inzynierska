﻿namespace Backend.Classes.Database
{
    public class AnswerDeck
    {
        public int Id { get; set; }
        public string Name { get; set; }

        // Foreign key to User
        public string UserId { get; set; }
        public DBUser User { get; set; }

        // Navigation property
        public ICollection<AnswerCard> AnswerCards { get; set; }
    }
}
