namespace Backend.Classes.DTO
{
    public class PlayedCardsDTO
    {
        public string Nickname { get; set; }
        public List<AnswerCardDTO> AnswerCard {  get; set; }
    }
}