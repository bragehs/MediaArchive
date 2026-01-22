using Microsoft.EntityFrameworkCore;
using MediaArchive.API.Models;

namespace MediaArchive.API.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Book> Books { get; set; }
    public DbSet<VideoGame> Games { get; set; }
}