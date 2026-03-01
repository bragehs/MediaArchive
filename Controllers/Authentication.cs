using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace MediaArchive.API.Controllers;

public record RegisterRequest(
    string UserName,
    string Email,
    string Password
);

public record LoginRequest(string UserName, string Password);

[ApiController]
[Route("auth")]
public class Authentication(UserManager<IdentityUser> userManager, IConfiguration config)
    : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var user = new IdentityUser { UserName = request.UserName, Email = request.Email };
        var result = await userManager.CreateAsync(user, request.Password);

        return result.Succeeded ? Ok() : BadRequest(result.Errors);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await userManager.FindByNameAsync(request.UserName);
        if (user == null || !await userManager.CheckPasswordAsync(user, request.Password))
            return Unauthorized();

        var token = GenerateJwt(user);
        return Ok(new { token });
    }

    [Authorize]
    [HttpGet("validate")]
    public IActionResult Validate()
    {
        return Ok();
    }

    private string GenerateJwt(IdentityUser user)
    {
        var jwtKey = config["Jwt:Key"];
        if (string.IsNullOrEmpty(jwtKey))
        {
            throw new InvalidOperationException("JWT Key is not configured.");
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: [new Claim(ClaimTypes.NameIdentifier, user.Id)],
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}