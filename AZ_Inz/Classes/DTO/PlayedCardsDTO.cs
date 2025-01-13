namespace AZ_Inz.Classes.DTO
{
    public class PlayedCardsDTO
    {
        public string Nickname { get; set; }
        public List<int> CardIds { get; set; }

        // New: so we can pass text to the front-end
        public List<AnswerCardDTO> AnswerCards { get; set; }
    }

}