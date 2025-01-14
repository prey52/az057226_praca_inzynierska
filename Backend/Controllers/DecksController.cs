using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Authorization;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Identity;
using Backend.Classes.Database;
using Backend.Classes.DTO;
using Microsoft.EntityFrameworkCore;

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

    [Authorize]
    [HttpPost("upload")]
    public async Task<IActionResult> UploadDeck([FromForm] DeckUploadDTO model)
    {

        var userId = this.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

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
                                var standardizedText = System.Text.RegularExpressions.Regex.Replace(
                                    text,
                                    "_+",
                                    "_____"
                                );

                                int blankSpaces = System.Text.RegularExpressions.Regex.Matches(
                                    standardizedText,
                                    "_____").Count;

                                if (blankSpaces == 0)
                                {
                                    blankSpaces = 1;
                                }

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
                return BadRequest("Invalid deck type. Allowed values are 'Questions' or 'Answers'.");
            }
        }

        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    [HttpGet("all-decks")]
    public IActionResult GetAllDecks()
    {
        var answerDecks = _context.AnswerDecks
            .Select(ad => new AnswerDeckDTO
            {
                Id = ad.Id,
                Name = ad.Name
            })
            .ToList();

        var questionDecks = _context.QuestionDecks
            .Select(qd => new QuestionDeckDTO
            {
                Id = qd.Id,
                Name = qd.Name
            })
            .ToList();

        return Ok(new AvailableDecksDto
        {
            AnswerDecks = answerDecks,
            QuestionDecks = questionDecks
        });
    }

    [HttpGet("{deckType}/{deckId}")]
    [Authorize]
    public async Task<IActionResult> GetDeckCards(string deckType, int deckId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (deckType == "Answers")
        {
            var cards = await _context.AnswerCards
                .Where(c => c.AnswerDeckId == deckId && c.AnswerDeck.UserId == userId)
                .Select(c => new CardDbDTO { Id = c.Id, Text = c.Text })
                .ToListAsync();
            return Ok(cards);
        }
        else if (deckType == "Questions")
        {
            var cards = await _context.QuestionCards
                .Where(c => c.QuestionDeckId == deckId && c.QuestionDeck.UserId == userId)
                .Select(c => new CardDbDTO { Id = c.Id, Text = c.Text })
                .ToListAsync();
            return Ok(cards);
        }

        return BadRequest("Invalid deck type.");
    }

    [HttpPut("{deckType}/{deckId}")]
    [Authorize]
    public async Task<IActionResult> UpdateDeckCards(
    string deckType,
    int deckId,
    [FromBody] List<CardDbDTO> updatedCards)
    {
        // 1) Identify the current user
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized("No valid user context found.");

        // 2) Handle logic for "Answers" deck vs. "Questions" deck
        if (deckType.Equals("Answers", StringComparison.OrdinalIgnoreCase))
        {
            // (A) Fetch existing answer cards that belong to the user & matching deck
            var existingDbCards = await _context.AnswerCards
                .Where(c => c.AnswerDeckId == deckId && c.AnswerDeck.UserId == userId)
                .ToListAsync();

            // (B) Process updates or deletions of existing cards
            foreach (var dbCard in existingDbCards)
            {
                var matching = updatedCards.FirstOrDefault(c => c.Id == dbCard.Id);
                if (matching == null)
                {
                    // If no match => user removed this card => remove from DB
                    _context.AnswerCards.Remove(dbCard);
                }
                else
                {
                    // If matched => update text
                    dbCard.Text = matching.Text;
                }
            }

            // (C) Process inserts (new cards with Id == 0)
            var newAnswerCardDtos = updatedCards.Where(c => c.Id == 0).ToList();
            foreach (var newDto in newAnswerCardDtos)
            {
                var newEntity = new AnswerCard
                {
                    Text = newDto.Text,
                    AnswerDeckId = deckId
                };
                _context.AnswerCards.Add(newEntity);
            }
        }
        else if (deckType.Equals("Questions", StringComparison.OrdinalIgnoreCase))
        {
            // (A) Fetch existing question cards
            var existingDbCards = await _context.QuestionCards
                .Where(c => c.QuestionDeckId == deckId && c.QuestionDeck.UserId == userId)
                .ToListAsync();

            // (B) Process updates or deletions
            foreach (var dbCard in existingDbCards)
            {
                var matching = updatedCards.FirstOrDefault(c => c.Id == dbCard.Id);
                if (matching == null)
                {
                    // no match => remove
                    _context.QuestionCards.Remove(dbCard);
                }
                else
                {
                    // update text & number
                    var standardizedText = System.Text.RegularExpressions.Regex.Replace(
                        matching.Text,
                        "_+",
                        "_____"
                    );
                    int blankSpaces = System.Text.RegularExpressions.Regex.Matches(
                        standardizedText,
                        "_____").Count;
                    if (blankSpaces == 0) blankSpaces = 1;

                    dbCard.Text = standardizedText;
                    dbCard.Number = blankSpaces;
                }
            }

            // (C) Process inserts for new question cards
            var newQuestionCardDtos = updatedCards.Where(c => c.Id == 0).ToList();
            foreach (var newDto in newQuestionCardDtos)
            {
                // standardize underscores & count
                var standardizedText = System.Text.RegularExpressions.Regex.Replace(
                    newDto.Text,
                    "_+",
                    "_____"
                );
                int blankSpaces = System.Text.RegularExpressions.Regex.Matches(
                    standardizedText,
                    "_____").Count;
                if (blankSpaces == 0) blankSpaces = 1;

                var newEntity = new QuestionCard
                {
                    Text = standardizedText,
                    Number = blankSpaces,
                    QuestionDeckId = deckId
                };
                _context.QuestionCards.Add(newEntity);
            }
        }
        else
        {
            return BadRequest("Invalid deck type. Must be 'Answers' or 'Questions'.");
        }

        // 3) Commit all changes
        await _context.SaveChangesAsync();

        return Ok("Deck updated successfully.");
    }


    [HttpDelete("{deckType}/{deckId}")]
    [Authorize]
    public async Task<IActionResult> DeleteDeck(string deckType, int deckId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (deckType == "Answers")
        {
            var deck = await _context.AnswerDecks.FirstOrDefaultAsync(d => d.Id == deckId && d.UserId == userId);
            if (deck == null) return NotFound("Deck not found or you don't have access to it.");

            _context.AnswerDecks.Remove(deck);
        }
        else if (deckType == "Questions")
        {
            var deck = await _context.QuestionDecks.FirstOrDefaultAsync(d => d.Id == deckId && d.UserId == userId);
            if (deck == null) return NotFound("Deck not found or you don't have access to it.");

            _context.QuestionDecks.Remove(deck);
        }
        else
        {
            return BadRequest("Invalid deck type.");
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = "Deck deleted successfully." });
    }

}

