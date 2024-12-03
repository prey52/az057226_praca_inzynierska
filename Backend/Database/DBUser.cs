using Microsoft.AspNetCore.Identity;

namespace Backend.Database
{
	public class DBUser : IdentityUser
	{
		public ICollection<Deck> Decks { get; set; }
	}

}
