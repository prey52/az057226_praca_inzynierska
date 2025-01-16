using Backend.Classes.Database;
using Backend.Classes.DTO;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<DBUser> _userManager;
        private readonly IConfiguration _configuration;

        public AuthController(UserManager<DBUser> userManager, IConfiguration configuration)
        {
            _userManager = userManager;
            _configuration = configuration;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginModelDTO model)
        {
            var user = await _userManager.FindByNameAsync(model.Username);
            if (user != null && await _userManager.CheckPasswordAsync(user, model.Password))
            {
                var token = await GenerateJwtToken(user);

                return Ok(new
                {
                    token,
                    expiration = DateTime.UtcNow.AddDays(1)
                });
            }
            return Unauthorized();
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterModelDTO model)
        {
            try
            {
                if (await _userManager.FindByNameAsync(model.Username) != null)
                {
                    return BadRequest(new { message = "Username already exists" });
                }

                if (await _userManager.FindByEmailAsync(model.Email) != null)
                {
                    return BadRequest(new { message = "Email already exists" });
                }

                var user = new DBUser { UserName = model.Username, Email = model.Email };
                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    return Ok();
                }

                return BadRequest(new
                {
                    message = "Registration failed",
                    errors = result.Errors.Select(e => e.Description)
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during registration: {ex.Message}");
                return StatusCode(500, new { message = "An unexpected error occurred during registration." });
            }
        }

        private async Task<string> GenerateJwtToken(DBUser user)
        {
            try
            {
                var claims = new[]
                {
                new Claim(ClaimTypes.Name, user.UserName), // Username
                new Claim(ClaimTypes.NameIdentifier, user.Id), // User ID (unique identifier)
                new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty), // Email
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()), // Unique JWT ID
                new Claim(JwtRegisteredClaimNames.Aud, _configuration["Jwt:Issuer"])
                };

                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
                if (key == null || key.KeySize == 0)
                {
                    throw new InvalidOperationException("JWT signing key is missing or invalid.");
                }

                var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

                var token = new JwtSecurityToken(
                    issuer: _configuration["Jwt:Issuer"],
                    audience: _configuration["Jwt:Audience"],
                    claims: claims,
                    expires: DateTime.UtcNow.AddDays(1),
                    signingCredentials: creds
                );

                return new JwtSecurityTokenHandler().WriteToken(token);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating JWT token: {ex.Message}");
                throw;
            }
        }
    }

    


    
}
