using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using MediaArchive.API.Models;

namespace MediaArchive.API.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<Book> Books { get; set; }
    public DbSet<VideoGame> Games { get; set; }
}