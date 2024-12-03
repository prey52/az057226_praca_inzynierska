namespace Backend.Database
{
	public class Card
	{
		public int CardId { get; set; }
		public string Description { get; set; }

		//Foreign key
		public int DeckId { get; set; }
		public Deck Deck { get; set; }
		}
}
