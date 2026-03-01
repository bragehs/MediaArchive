using MediaArchive.API.Data;
using MediaArchive.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MediaArchive.API.Controllers;

public record BookSearchRequest(string Chars);

public record VideoGameSearchRequest(string Chars);

[ApiController]
[Route("api/[controller]")]
public class MediaSearch(AppDbContext context) : ControllerBase
{
    private const int PageSize = 10;

    [HttpGet("books")]
    public async Task<IActionResult> GetBookResults([FromQuery] BookSearchRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Chars)) return Ok(new List<Book>());
        var results = await context.Books
            .AsNoTracking()
            .Where(b => b.Title.Contains(request.Chars) || b.Author.Contains(request.Chars))
            .Take(PageSize)
            .ToListAsync();

        return Ok(results);
    }

    [HttpGet("games")]
    public async Task<IActionResult> GetVideoGameResults([FromQuery] VideoGameSearchRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Chars)) return Ok(new List<VideoGame>());
        var results = await context.Games
            .AsNoTracking()
            .Where(b => b.Title.Contains(request.Chars) || b.Developer.Contains(request.Chars))
            .Take(PageSize)
            .ToListAsync();

        return Ok(results);
    }
}