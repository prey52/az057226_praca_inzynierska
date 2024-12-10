using Backend.Database;
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
        public async Task<IActionResult> Login([FromBody] LoginModel model)
        {
            var user = await _userManager.FindByNameAsync(model.Username);
            if (user != null && await _userManager.CheckPasswordAsync(user, model.Password))
            {
                // Generate JWT Token
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
        public async Task<IActionResult> Register([FromBody] RegisterModel model)
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
                    var token = await GenerateJwtToken(user); // Generate JWT Token after successful registration
                    return Ok(new { token });
                }

                return BadRequest(new
                {
                    message = "Registration failed",
                    errors = result.Errors.Select(e => e.Description)
                });
            }
            catch (Exception ex)
            {
                // Log exception to understand what failed
                Console.WriteLine($"Error during registration: {ex.Message}");
                return StatusCode(500, new { message = "An unexpected error occurred during registration." });
            }
        }

        private async Task<string> GenerateJwtToken(DBUser user)
        {
            try
            {
                // Add the claims for both registration and login
                var claims = new[]
                {
            new Claim(ClaimTypes.Name, user.UserName), // Username
            new Claim(ClaimTypes.NameIdentifier, user.Id), // User ID (unique identifier)
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty), // Email
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()) // Unique JWT ID
        };

                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
                if (key == null || key.KeySize == 0)
                {
                    throw new InvalidOperationException("JWT signing key is missing or invalid.");
                }

                var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

                var token = new JwtSecurityToken(
                    _configuration["Jwt:Issuer"],
                    _configuration["Jwt:Issuer"],
                    claims,
                    expires: DateTime.UtcNow.AddDays(1),
                    signingCredentials: creds
                );

                return new JwtSecurityTokenHandler().WriteToken(token);
            }
            catch (Exception ex)
            {
                // Log the error
                Console.WriteLine($"Error generating JWT token: {ex.Message}");
                throw;
            }
        }
    }

    public class RegisterModel
    {
        public string Username { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
    }


    public class LoginModel
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }
}
