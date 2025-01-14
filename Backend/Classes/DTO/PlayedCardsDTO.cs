namespace Backend.Classes.DTO
{
    public class PlayedCardsDTO
    {
        public string Nickname { get; set; }
        public List<int> CardIds { get; set; }
        public List<AnswerCardDTO> AnswerCards { get; set; }
    }

}