using Microsoft.EntityFrameworkCore;

namespace Backend.Database
{
	public class Deck
	{
		public int DeckId { get; set; }
		public string Name { get; set; }
		public bool IsQuestionDeck { get; set; } //True: questions, False: answers

		//Foreign key
		public string UserId { get; set; }
		public DBUser User { get; set; }
		public ICollection<Card> Cards { get; set; }
	}

}
