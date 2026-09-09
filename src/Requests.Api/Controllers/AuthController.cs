using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace Requests.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    // משתמשים hard-coded לצורך demo.
    // בפרודקשן: טבלת Users במסד הנתונים עם סיסמאות מוצפנות (BCrypt).
    private static readonly IReadOnlyList<DemoUser> Users = new[]
    {
        new DemoUser(UserId: 1, Password: "user1",  IsAdmin: false),
        new DemoUser(UserId: 2, Password: "user2",  IsAdmin: false),
        new DemoUser(UserId: 3, Password: "admin",  IsAdmin: true),
    };

    private readonly IConfiguration _config;

    public AuthController(IConfiguration config)
    {
        _config = config;
    }

    /// <summary>
    /// מחזיר JWT token חתום.
    /// userId + password → token עם claims: userId, isAdmin.
    /// </summary>
    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        if (request is null || request.UserId <= 0 || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest("userId and password are required.");

        var user = Users.FirstOrDefault(u =>
            u.UserId == request.UserId &&
            u.Password == request.Password);

        if (user is null)
            return Unauthorized("Invalid credentials.");

        var token = GenerateToken(user);
        return Ok(new { token });
    }

    private string GenerateToken(DemoUser user)
    {
        var secretKey = _config["Jwt:SecretKey"]
            ?? throw new InvalidOperationException("Jwt:SecretKey is not configured.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim("userId",  user.UserId.ToString()),
            new Claim("isAdmin", user.IsAdmin.ToString().ToLower()),
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddHours(24),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private record DemoUser(int UserId, string Password, bool IsAdmin);
}

public record LoginRequest(int UserId, string Password);
