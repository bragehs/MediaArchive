using MediaArchive.Models;

namespace MediaArchive.Data;

public static class DbSeeder
{
    public static void Seed(AppDbContext db)
    {
        if (db.MediaItems.Any()) return;

        Genre G(string name) => new() { Name = name };
        var fantasy = G("Fantasy");
        var sciFi = G("Sci-Fi");
        var horror = G("Horror");
        var mystery = G("Mystery");
        var rpg = G("RPG");
        var adventure = G("Adventure");
        var drama = G("Drama");
        var heist = G("Heist");
        var comingOfAge = G("Coming of Age");
        var action = G("Action");
        db.Genres.AddRange(fantasy, sciFi, horror, mystery, rpg, adventure, drama, heist, comingOfAge, action);

        var witcherverse = new Universe { Name = "The Witcher", LoreOfTheDayPrompt = "Tell me an obscure fact from the Continent." };
        var middleEarth = new Universe { Name = "Middle-earth", LoreOfTheDayPrompt = "Share a piece of Tolkien deep lore." };
        db.Universes.AddRange(witcherverse, middleEarth);

        static string Cover(string isbn) => $"https://covers.openlibrary.org/b/isbn/{isbn}-L.jpg";

        static MediaItem WithGenres(MediaItem item, params Genre[] genres)
        {
            item.Genres = genres.Select(g => new MediaItemGenre { Genre = g }).ToList();
            return item;
        }

        var hobbit = (Book)WithGenres(new Book { Title = "The Hobbit", Author = "J.R.R. Tolkien", ReleaseYear = 1937, ImageUrl = Cover("9780547928227"), Universe = middleEarth, Description = "A hobbit is swept into a quest to reclaim a mountain of treasure from a dragon." }, fantasy, adventure);
        var lotr = (Book)WithGenres(new Book { Title = "The Lord of the Rings", Author = "J.R.R. Tolkien", ReleaseYear = 1954, ImageUrl = Cover("9780544003415"), Universe = middleEarth, Description = "The one ring must be destroyed in the fires of Mount Doom." }, fantasy, adventure);
        var wayOfKings = (Book)WithGenres(new Book { Title = "The Way of Kings", Author = "Brandon Sanderson", ReleaseYear = 2010, ImageUrl = Cover("9780765326355"), Description = "Epic fantasy on the storm-wracked world of Roshar." }, fantasy);
        var dune = (Book)WithGenres(new Book { Title = "Dune", Author = "Frank Herbert", ReleaseYear = 1965, ImageUrl = Cover("9780441013593"), Description = "Political intrigue and prophecy on the desert planet Arrakis." }, sciFi);
        var hailMary = (Book)WithGenres(new Book { Title = "Project Hail Mary", Author = "Andy Weir", ReleaseYear = 2021, ImageUrl = Cover("9780593135204"), Description = "A lone astronaut must save Earth, and he can't remember why he's there." }, sciFi);
        var shining = (Book)WithGenres(new Book { Title = "The Shining", Author = "Stephen King", ReleaseYear = 1977, ImageUrl = Cover("9780307743657"), Description = "A haunted hotel preys on a family over a long winter." }, horror);
        var silentPatient = (Book)WithGenres(new Book { Title = "The Silent Patient", Author = "Alex Michaelides", ReleaseYear = 2019, ImageUrl = Cover("9781250301697"), Description = "A woman shoots her husband and never speaks again." }, mystery);
        var lastWish = (Book)WithGenres(new Book { Title = "The Last Wish", Author = "Andrzej Sapkowski", ReleaseYear = 1993, ImageUrl = Cover("9780316029186"), Universe = witcherverse, Description = "The short stories that introduce Geralt of Rivia." }, fantasy);

        var botw = (Game)WithGenres(new Game { Title = "The Legend of Zelda: Breath of the Wild", Developer = "Nintendo", ReleaseYear = 2017, Description = "Open-air adventure across the ruined kingdom of Hyrule." }, adventure, rpg);
        var elden = (Game)WithGenres(new Game { Title = "Elden Ring", Developer = "FromSoftware", ReleaseYear = 2022, Description = "A brutal open-world action RPG in the Lands Between." }, rpg, action);
        var witcher3 = (Game)WithGenres(new Game { Title = "The Witcher 3: Wild Hunt", Developer = "CD Projekt Red", ReleaseYear = 2015, Universe = witcherverse, Description = "Geralt hunts for his adopted daughter across a war-torn world." }, rpg, fantasy);
        var hades = (Game)WithGenres(new Game { Title = "Hades", Developer = "Supergiant Games", ReleaseYear = 2020, Description = "Fight out of the underworld in a roguelike built on Greek myth." }, action, rpg);
        var hollow = (Game)WithGenres(new Game { Title = "Hollow Knight", Developer = "Team Cherry", ReleaseYear = 2017, Description = "Explore a vast ruined kingdom of insects and heroes." }, adventure);
        var stardew = (Game)WithGenres(new Game { Title = "Stardew Valley", Developer = "ConcernedApe", ReleaseYear = 2016, Description = "Inherit a farm and build a new life in the valley." }, adventure);

        var blade = (Movie)WithGenres(new Movie { Title = "Blade Runner 2049", Director = "Denis Villeneuve", ReleaseYear = 2017, Description = "A young blade runner uncovers a secret that could shatter society." }, sciFi, drama);
        var inception = (Movie)WithGenres(new Movie { Title = "Inception", Director = "Christopher Nolan", ReleaseYear = 2010, Description = "A thief steals secrets from dreams — and plants one." }, sciFi, heist);
        var parasite = (Movie)WithGenres(new Movie { Title = "Parasite", Director = "Bong Joon-ho", ReleaseYear = 2019, Description = "A poor family schemes their way into a wealthy household." }, drama, mystery);
        var grand = (Movie)WithGenres(new Movie { Title = "The Grand Budapest Hotel", Director = "Wes Anderson", ReleaseYear = 2014, Description = "A concierge and a lobby boy tangle over a priceless painting." }, comingOfAge, heist);

        var wire = (Show)WithGenres(new Show { Title = "The Wire", Studio = "HBO", ReleaseYear = 2002, Description = "Crime and institutions in Baltimore, seen from every side." }, drama, mystery);
        var severance = (Show)WithGenres(new Show { Title = "Severance", Studio = "Apple TV+", ReleaseYear = 2022, Description = "Office workers surgically divide their work and personal memories." }, sciFi, mystery);
        var arcane = (Show)WithGenres(new Show { Title = "Arcane", Studio = "Fortiche", ReleaseYear = 2021, Description = "Two sisters end up on opposite sides of a brewing war." }, fantasy, drama);

        var fma = (Anime)WithGenres(new Anime { Title = "Fullmetal Alchemist: Brotherhood", Studio = "Bones", ReleaseYear = 2009, Description = "Two brothers seek the Philosopher's Stone to restore their bodies." }, fantasy, adventure);
        var frieren = (Anime)WithGenres(new Anime { Title = "Frieren: Beyond Journey's End", Studio = "Madhouse", ReleaseYear = 2023, Description = "An elven mage reflects on mortality after her party's quest ends." }, fantasy, drama);
        var cowboy = (Anime)WithGenres(new Anime { Title = "Cowboy Bebop", Studio = "Sunrise", ReleaseYear = 1998, Description = "Bounty hunters drift through a lived-in future solar system." }, sciFi, action);

        var all = new MediaItem[]
        {
            hobbit, lotr, wayOfKings, dune, hailMary, shining, silentPatient, lastWish,
            botw, elden, witcher3, hades, hollow, stardew,
            blade, inception, parasite, grand,
            wire, severance, arcane,
            fma, frieren, cowboy
        };
        db.MediaItems.AddRange(all);

        void Log(MediaItem item, MediaStatus status, int? rating, bool fav,
                 (int y, int m, int d)? start = null, (int y, int m, int d)? end = null,
                 int? effort = null, string? tags = null, params ConsumptionEntry[] extra)
        {
            var umi = new UserMediaItem
            {
                MediaItem = item,
                Status = status,
                Rating = rating,
                IsFavorite = fav,
                PersonalTags = tags,
                AddedDate = new DateOnly(2025, 1, 1).AddDays(Random.Shared.Next(0, 400))
            };
            var entries = new List<ConsumptionEntry>();
            if (status is MediaStatus.Completed or MediaStatus.InProgress)
            {
                entries.Add(new ConsumptionEntry
                {
                    StartDate = start is { } s ? new DateOnly(s.y, s.m, s.d) : null,
                    EndDate = end is { } e ? new DateOnly(e.y, e.m, e.d) : null,
                    RatingAtTime = rating,
                    Effort = effort
                });
            }
            entries.AddRange(extra);
            umi.Entries = entries;
            db.UserMediaItems.Add(umi);
        }

        Log(hobbit, MediaStatus.Completed, 9, true, (2024, 11, 1), (2024, 11, 12), 310, "comfort-read",
            new ConsumptionEntry { StartDate = new DateOnly(2025, 6, 1), EndDate = new DateOnly(2025, 6, 8), RatingAtTime = 8, Effort = 310, Notes = "Still cosy, a touch slower than I remembered." });
        Log(lotr, MediaStatus.Completed, 10, true, (2025, 1, 10), (2025, 2, 20), 1178, "epic");
        Log(dune, MediaStatus.Completed, 9, false, (2025, 3, 1), (2025, 3, 25), 688);
        Log(hailMary, MediaStatus.InProgress, null, false, (2025, 9, 1), null, 120, "current");
        Log(wayOfKings, MediaStatus.Interested, null, false);
        Log(shining, MediaStatus.Completed, 7, false, (2025, 4, 3), (2025, 4, 14), 447);
        Log(silentPatient, MediaStatus.Dropped, 4, false, (2025, 5, 1), null, 90, "dnf");
        Log(lastWish, MediaStatus.Interested, null, false);

        Log(botw, MediaStatus.Completed, 10, true, (2024, 12, 20), (2025, 1, 30), 90, "goty");
        Log(elden, MediaStatus.Completed, 9, true, (2025, 2, 1), (2025, 3, 10), 110);
        Log(witcher3, MediaStatus.InProgress, null, false, (2025, 9, 15), null, 40, "current");
        Log(hades, MediaStatus.Completed, 9, false, (2025, 5, 5), (2025, 6, 1), 45);
        Log(hollow, MediaStatus.Interested, null, false);
        Log(stardew, MediaStatus.Completed, 8, false, (2025, 1, 5), (2025, 2, 1), 60);

        Log(blade, MediaStatus.Completed, 9, true, (2025, 7, 2), (2025, 7, 2), 164);
        Log(inception, MediaStatus.Completed, 8, false, (2025, 3, 18), (2025, 3, 18), 148);
        Log(parasite, MediaStatus.Completed, 10, true, (2025, 8, 9), (2025, 8, 9), 132);
        Log(grand, MediaStatus.Interested, null, false);

        Log(wire, MediaStatus.InProgress, null, false, (2025, 8, 1), null, null, "current");
        Log(severance, MediaStatus.Completed, 9, true, (2025, 6, 10), (2025, 6, 25), null);
        Log(arcane, MediaStatus.Completed, 9, false, (2025, 4, 20), (2025, 5, 2), null);

        Log(fma, MediaStatus.Completed, 10, true, (2024, 10, 1), (2024, 11, 1), null, "favourite");
        Log(frieren, MediaStatus.Completed, 10, true, (2025, 5, 15), (2025, 6, 20), null);
        Log(cowboy, MediaStatus.Interested, null, false);

        db.SaveChanges();
    }
}
