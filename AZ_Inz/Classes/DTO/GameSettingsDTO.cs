﻿namespace AZ_Inz.Classes.DTO
{
    public class GameSettingsDTO
    {
        public string lobbyID {  get; set; }
        public int ScoreToWin { get; set; }
        public List<AnswerDeckDTO> ChosenAnswersDecks {  get; set; }
        public List<QuestionDeckDTO> ChosenQuestionsDecks {  get; set; }
    }
}