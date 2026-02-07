using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace MediaArchive.API.Controllers;

[ApiController]
[Route("auth")]
public class Authentication(UserManager<IdentityUser> userManager, IConfiguration config)
    : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(string email, string password)
    {
        var user = new IdentityUser { UserName = email, Email = email };
        var result = await userManager.CreateAsync(user, password);

        return result.Succeeded ? Ok() : BadRequest(result.Errors);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(string email, string password)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user == null || !await userManager.CheckPasswordAsync(user, password))
            return Unauthorized();

        var token = GenerateJwt(user);
        return Ok(new { token });
    }

    private string GenerateJwt(IdentityUser user)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(config["JwtKey"] ?? string.Empty)
        );

        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            claims: [new Claim(ClaimTypes.NameIdentifier, user.Id)],
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}