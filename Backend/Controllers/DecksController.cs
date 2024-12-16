using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Backend.Database;
using System;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using System.IO;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Authorization;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Identity;

[Route("api/[controller]")]
//[Authorize]
[ApiController]
public class DecksController : ControllerBase
{
    private readonly CardsDBContext _context;
    private readonly UserManager<DBUser> _userManager;

    public DecksController(CardsDBContext context, UserManager<DBUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    private async Task<string> GetUserIdFromToken(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        try
        {
            var jwtToken = handler.ReadJwtToken(token);
            var userEmail = jwtToken?.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Email)?.Value;
            DBUser user = await _userManager.FindByEmailAsync(userEmail);

            return user.Id;
        }
        catch (Exception)
        {
            return null;
        }
    }


    [HttpPost("upload")]
    public async Task<IActionResult> UploadDeck([FromForm] DeckUploadDto model)
    {
        var token = HttpContext.Request.Headers["Authorization"].ToString().Replace("Bearer ", "").Trim();
        var userId = await GetUserIdFromToken(token);


        if (string.IsNullOrEmpty(model.DeckName) || string.IsNullOrEmpty(model.DeckType))
        {
            return BadRequest("Deck name and type are required.");
        }

        if (model.File == null || model.File.Length == 0)
        {
            return BadRequest("A valid .docx file is required.");
        }

        try
        {
            if (model.DeckType == "Questions")
            {
                var deck = new QuestionDeck
                {
                    Name = model.DeckName,
                    UserId = userId,
                    QuestionCards = new List<QuestionCard>()
                };

                using (var stream = model.File.OpenReadStream())
                {
                    using (var wordDoc = WordprocessingDocument.Open(stream, false))
                    {
                        var body = wordDoc.MainDocumentPart.Document.Body;

                        foreach (var paragraph in body.Elements<Paragraph>())
                        {
                            var text = paragraph.InnerText.Trim();
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                // Check for and standardize blank spaces ("_____")
                                var standardizedText = System.Text.RegularExpressions.Regex.Replace(
                                    text,                  // Input text
                                    "_+",                  // Match one or more underscores
                                    "_____"                // Replace with exactly 5 underscores
                                );

                                // Count occurrences of "_____" (blank spaces)
                                int blankSpaces = System.Text.RegularExpressions.Regex.Matches(
                                    standardizedText,      // Input text
                                    "_____").Count;        // Pattern for 5 underscores

                                // If no blank spaces, set Number to 1
                                if (blankSpaces == 0)
                                {
                                    blankSpaces = 1;
                                }

                                // Add the processed QuestionCard to the deck
                                deck.QuestionCards.Add(new QuestionCard
                                {
                                    Text = standardizedText,
                                    Number = blankSpaces
                                });
                            }
                        }
                    }
                }

                _context.QuestionDecks.Add(deck);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Questions deck uploaded successfully." });
            }


            else if (model.DeckType == "Answers")
            {
                var deck = new AnswerDeck
                {
                    Name = model.DeckName,
                    UserId = userId,
                    AnswerCards = new List<AnswerCard>()
                };

                using (var stream = model.File.OpenReadStream())
                {
                    using (var wordDoc = WordprocessingDocument.Open(stream, false))
                    {
                        var body = wordDoc.MainDocumentPart.Document.Body;

                        foreach (var paragraph in body.Elements<Paragraph>())
                        {
                            var text = paragraph.InnerText.Trim();
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                deck.AnswerCards.Add(new AnswerCard
                                {
                                    Text = text
                                });
                            }
                        }
                    }
                }

                _context.AnswerDecks.Add(deck);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Answers deck uploaded successfully." });
            }

            else
            {
                // Return an error if the deck type is invalid
                return BadRequest("Invalid deck type. Allowed values are 'Questions' or 'Answers'.");
            }
        }

        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

}

public class DeckUploadDto
{
    [Required]
    public string DeckName { get; set; }

    [Required]
    public string DeckType { get; set; } // "questions" or "answers"

    [Required]
    public IFormFile File { get; set; }
}
