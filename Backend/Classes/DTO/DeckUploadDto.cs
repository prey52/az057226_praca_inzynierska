using System.ComponentModel.DataAnnotations;

namespace Backend.Classes.DTO
{
    public class DeckUploadDto
    {
        [Required]
        public string DeckName { get; set; }

        [Required]
        public string DeckType { get; set; } // "questions" or "answers"

        [Required]
        public IFormFile File { get; set; }
    }
}
