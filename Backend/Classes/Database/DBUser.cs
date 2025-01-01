using Microsoft.AspNetCore.Identity;

namespace Backend.Classes.Database
{
    public class DBUser : IdentityUser
    {
        public ICollection<QuestionDeck> QuestionDecks { get; set; }
        public ICollection<AnswerDeck> AnswerDecks { get; set; }
    }

}
