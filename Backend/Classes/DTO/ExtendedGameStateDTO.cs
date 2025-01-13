namespace Backend.Classes.DTO
{
	public class ExtendedGameStateDTO
	{
		public QuestionCardDTO CurrentQuestion { get; set; }
		public string CurrentCzar { get; set; }
		public List<Player> Players { get; set; } // each has Nickname + Score
		public List<AnswerCardDTO> MyHand { get; set; } // specifically for the caller
	}


}
