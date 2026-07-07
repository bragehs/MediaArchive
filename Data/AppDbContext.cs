using MediaArchive.Models;
using Microsoft.EntityFrameworkCore;

namespace MediaArchive.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<MediaItem> MediaItems => Set<MediaItem>();
    public DbSet<Book> Books => Set<Book>();
    public DbSet<Game> Games => Set<Game>();
    public DbSet<Movie> Movies => Set<Movie>();
    public DbSet<Show> Shows => Set<Show>();
    public DbSet<Anime> Anime => Set<Anime>();

    public DbSet<Genre> Genres => Set<Genre>();
    public DbSet<Universe> Universes => Set<Universe>();
    public DbSet<UserMediaItem> UserMediaItems => Set<UserMediaItem>();
    public DbSet<ConsumptionEntry> ConsumptionEntries => Set<ConsumptionEntry>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<MediaItem>()
            .HasDiscriminator(m => m.MediaType)
            .HasValue<Book>(MediaType.Book)
            .HasValue<Game>(MediaType.Game)
            .HasValue<Movie>(MediaType.Movie)
            .HasValue<Show>(MediaType.Show)
            .HasValue<Anime>(MediaType.Anime);

        builder.Entity<UserMediaItem>()
            .HasIndex(u => u.MediaItemId)
            .IsUnique();

        builder.Entity<UserMediaItem>()
            .HasOne(u => u.MediaItem)
            .WithOne(m => m.UserMediaItem)
            .HasForeignKey<UserMediaItem>(u => u.MediaItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<MediaItemGenre>()
            .HasKey(mg => new { mg.MediaItemId, mg.GenreId });

        builder.Entity<Genre>()
            .HasIndex(g => g.Name)
            .IsUnique();
    }
}
