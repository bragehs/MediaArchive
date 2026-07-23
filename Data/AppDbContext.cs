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

    public DbSet<Genre> Genres => Set<Genre>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<Person> People => Set<Person>();
    public DbSet<Universe> Universes => Set<Universe>();
    public DbSet<Series> Series => Set<Series>();
    public DbSet<UserMediaItem> UserMediaItems => Set<UserMediaItem>();
    public DbSet<ConsumptionEntry> ConsumptionEntries => Set<ConsumptionEntry>();
    public DbSet<EntryNote> EntryNotes => Set<EntryNote>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<MediaItem>()
            .HasDiscriminator(m => m.MediaType)
            .HasValue<Book>(MediaType.Book)
            .HasValue<Game>(MediaType.Game)
            .HasValue<Movie>(MediaType.Movie)
            .HasValue<Show>(MediaType.Show);

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

        builder.Entity<MediaItemTag>()
            .HasKey(mt => new { mt.MediaItemId, mt.TagId });

        builder.Entity<Tag>()
            .HasIndex(t => t.Name)
            .IsUnique();

        // One person can hold several roles on one item, so Role is part of the key.
        builder.Entity<MediaItemCredit>()
            .HasKey(mc => new { mc.MediaItemId, mc.PersonId, mc.Role });

        builder.Entity<Person>()
            .HasIndex(p => p.Name)
            .IsUnique();

        builder.Entity<Universe>()
            .HasIndex(u => u.Name)
            .IsUnique();

        builder.Entity<Series>()
            .HasIndex(s => s.Name)
            .IsUnique();

        // Required: an item is always in a series, "Standalone" if nothing else.
        // Restrict so a series can't be deleted out from under its items.
        builder.Entity<MediaItem>()
            .HasOne(m => m.Series)
            .WithMany(s => s.Items)
            .HasForeignKey(m => m.SeriesId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Series>()
            .HasData(new Series
            {
                Id = Models.Series.StandaloneId,
                Name = Models.Series.StandaloneName
            });

        // Self-referencing genre hierarchy. Restrict: a genre with subgenres
        // can't be deleted out from under them.
        builder.Entity<Genre>()
            .HasOne(g => g.ParentGenre)
            .WithMany(g => g.Subgenres)
            .HasForeignKey(g => g.ParentGenreId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
