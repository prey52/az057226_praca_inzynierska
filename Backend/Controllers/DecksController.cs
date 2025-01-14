﻿using Microsoft.AspNetCore.Mvc;
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
    public async Task<IActionResult> UpdateDeckCards(string deckType, int deckId, [FromBody] List<CardDbDTO> updatedCards)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (deckType == "Answers")
        {
            var cards = await _context.AnswerCards
                .Where(c => c.AnswerDeckId == deckId && c.AnswerDeck.UserId == userId)
                .ToListAsync();

            foreach (var card in cards)
            {
                var updatedCard = updatedCards.FirstOrDefault(c => c.Id == card.Id);
                if (updatedCard != null)
                {
                    card.Text = updatedCard.Text;
                }
            }
        }
        else if (deckType == "Questions")
        {
            var cards = await _context.QuestionCards
                .Where(c => c.QuestionDeckId == deckId && c.QuestionDeck.UserId == userId)
                .ToListAsync();

            foreach (var card in cards)
            {
                var updatedCard = updatedCards.FirstOrDefault(c => c.Id == card.Id);
                if (updatedCard != null)
                {
                    card.Text = updatedCard.Text;
                }
            }
        }
        else
        {
            return BadRequest("Invalid deck type.");
        }

        await _context.SaveChangesAsync();
        return Ok();
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

