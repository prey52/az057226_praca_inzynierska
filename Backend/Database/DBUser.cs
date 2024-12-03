using Microsoft.AspNetCore.Identity;

namespace Backend.Database
{
	public class DBUser : IdentityUser
	{
        public ICollection<QuestionDeck> QuestionDecks { get; set; }
        public ICollection<AnswerDeck> AnswerDecks { get; set; }
    }

}
