using MediaArchive.API.Models;

namespace MediaArchive.API.Data;

public static class DbSeeder
{
    public static void Seed(AppDbContext context)
    {
        SeedBooks(context);
        SeedVideoGames(context);
    }

    private static void SeedBooks(AppDbContext context)
    {
        if (context.Books.Any()) return; // Already seeded

        var books = new List<Book>
        {
            // Fantasy
            new() { Id = "978-0547928227", Title = "The Hobbit", Author = "J.R.R. Tolkien", ImageUrl = "https://images-na.ssl-images-amazon.com/images/I/71V2v2GtAtL.jpg", ReleaseYear = "1937" },
            new() { Id = "978-0544003415", Title = "The Lord of the Rings", Author = "J.R.R. Tolkien", ImageUrl = "https://images-na.ssl-images-amazon.com/images/I/51EstVXM1UL.jpg", ReleaseYear = "1954" },
            new() { Id = "978-0439708180", Title = "Harry Potter and the Sorcerer's Stone", Author = "J.K. Rowling", ImageUrl = "https://images-na.ssl-images-amazon.com/images/I/81YOuOGFCJL.jpg", ReleaseYear = "1997" },
            new() { Id = "978-0439064873", Title = "Harry Potter and the Chamber of Secrets", Author = "J.K. Rowling", ImageUrl = "https://images-na.ssl-images-amazon.com/images/I/81lAPl9Fl0L.jpg", ReleaseYear = "1998" },
            new() { Id = "978-0553573404", Title = "A Game of Thrones", Author = "George R.R. Martin", ImageUrl = "https://images-na.ssl-images-amazon.com/images/I/91dSMhdIzTL.jpg", ReleaseYear = "1996" },
            new() { Id = "978-0765326355", Title = "The Name of the Wind", Author = "Patrick Rothfuss", ImageUrl = "https://images-na.ssl-images-amazon.com/images/I/91b8oNwaH1L.jpg", ReleaseYear = "2007" },
            new() { Id = "978-0765350381", Title = "The Way of Kings", Author = "Brandon Sanderson", ImageUrl = "https://images-na.ssl-images-amazon.com/images/I/81UbXbRu3-L.jpg", ReleaseYear = "2010" },

            // Science Fiction
            new() { Id = "978-0441013593", Title = "Dune", Author = "Frank Herbert", ImageUrl = "https://images-na.ssl-images-amazon.com/images/I/81ym2zgFHIL.jpg", ReleaseYear = "1965" },
            new() { Id = "978-0345342966", Title = "Foundation", Author = "Isaac Asimov", ImageUrl = "https://images-na.ssl-images-amazon.com/images/I/81dh4CK9kWL.jpg", ReleaseYear = "1951" },
            new() { Id = "978-0441172719", Title = "Neuromancer", Author = "William Gibson", ImageUrl = "https://images-na.ssl-images-amazon.com/images/I/71xVDOPt6yL.jpg", ReleaseYear = "1984" },
            new() { Id = "978-0316769174", Title = "The Martian", Author = "Andy Weir", ImageUrl = "https://images-na.ssl-images-amazon.com/images/I/81R2pSEW4qL.jpg", ReleaseYear = "2011" },
            new() { Id = "978-0316015844", Title = "Ready Player One", Author = "Ernest Cline", ImageUrl = "https://images-na.ssl-images-amazon.com/images/I/81mfZ0lQ87L.jpg", ReleaseYear = "2011" },
            new() { Id = "978-1250178602", Title = "Project Hail Mary", Author = "Andy Weir", ImageUrl = "https://images-na.ssl-images-amazon.com/images/I/81FYkCxu1iL.jpg", ReleaseYear = "2021" },

            // Classic Literature
            new() { Id = "978-0451524935", Title = "1984", Author = "George Orwell", ImageUrl = "https://images-na.ssl-images-amazon.com/images/I/71kxa1-0mfL.jpg", ReleaseYear = "1949" },
            new() { Id = "978-0060850524", Title = "Brave New World", Author = "Aldous Huxley", ImageUrl = "https://images-na.ssl-images-amazon.com/images/I/81p3pDJvXxL.jpg", ReleaseYear = "1932" },
            new() { Id = "978-0061120084", Title = "To Kill a Mockingbird", Author = "Harper Lee", ImageUrl = "https://images-na.ssl-images-amazon.com/images/I/81gepf1eMqL.jpg", ReleaseYear = "1960" },
            new() { Id = "978-0743273565", Title = "The Great Gatsby", Author = "F. Scott Fitzgerald", ImageUrl = "https://images-na.ssl-images-amazon.com/images/I/81QuEGw8VPL.jpg", ReleaseYear = "1925" },
            new() { Id = "978-0142437247", Title = "Pride and Prejudice", Author = "Jane Austen", ImageUrl = "https://images-na.ssl-images-amazon.com/images/I/81NLDvyAHrL.jpg", ReleaseYear = "1813" },

            // Mystery/Thriller
            new() { Id = "978-0307588371", Title = "Gone Girl", Author = "Gillian Flynn", ImageUrl = "https://images-na.ssl-images-amazon.com/images/I/81a5KHEkwWL.jpg", ReleaseYear = "2012" },
            new() { Id = "978-0385514231", Title = "The Da Vinci Code", Author = "Dan Brown", ImageUrl = "https://images-na.ssl-images-amazon.com/images/I/815WORuYMML.jpg", ReleaseYear = "2003" },
            new() { Id = "978-0307949486", Title = "The Girl with the Dragon Tattoo", Author = "Stieg Larsson", ImageUrl = "https://images-na.ssl-images-amazon.com/images/I/81FqIKv8lEL.jpg", ReleaseYear = "2005" },
            new() { Id = "978-1501175466", Title = "The Silent Patient", Author = "Alex Michaelides", ImageUrl = "https://images-na.ssl-images-amazon.com/images/I/81bawHa7FjL.jpg", ReleaseYear = "2019" },

            // Non-Fiction
            new() { Id = "978-0735211292", Title = "Atomic Habits", Author = "James Clear", ImageUrl = "https://images-na.ssl-images-amazon.com/images/I/81YkqyaFVEL.jpg", ReleaseYear = "2018" },
            new() { Id = "978-1501124020", Title = "Sapiens", Author = "Yuval Noah Harari", ImageUrl = "https://images-na.ssl-images-amazon.com/images/I/713jIoMO3UL.jpg", ReleaseYear = "2011" },
            new() { Id = "978-0307887894", Title = "The Lean Startup", Author = "Eric Ries", ImageUrl = "https://images-na.ssl-images-amazon.com/images/I/81-QB7nDh4L.jpg", ReleaseYear = "2011" },
            new() { Id = "978-0062316097", Title = "Thinking, Fast and Slow", Author = "Daniel Kahneman", ImageUrl = "https://images-na.ssl-images-amazon.com/images/I/71UaVRc3bZL.jpg", ReleaseYear = "2011" },

            // Horror
            new() { Id = "978-0307743657", Title = "The Shining", Author = "Stephen King", ImageUrl = "https://images-na.ssl-images-amazon.com/images/I/91U7HNa2NQL.jpg", ReleaseYear = "1977" },
            new() { Id = "978-1501182099", Title = "It", Author = "Stephen King", ImageUrl = "https://images-na.ssl-images-amazon.com/images/I/71QNt6wRwEL.jpg", ReleaseYear = "1986" },
            new() { Id = "978-0143129486", Title = "Dracula", Author = "Bram Stoker", ImageUrl = "https://images-na.ssl-images-amazon.com/images/I/81DqMjenFuL.jpg", ReleaseYear = "1897" },

            // Contemporary Fiction
            new() { Id = "978-0385490818", Title = "The Kite Runner", Author = "Khaled Hosseini", ImageUrl = "https://images-na.ssl-images-amazon.com/images/I/81IzbD2IiIL.jpg", ReleaseYear = "2003" },
            new() { Id = "978-0316769488", Title = "The Catcher in the Rye", Author = "J.D. Salinger", ImageUrl = "https://images-na.ssl-images-amazon.com/images/I/81OthjkJBuL.jpg", ReleaseYear = "1951" },
            new() { Id = "978-0544272996", Title = "Life of Pi", Author = "Yann Martel", ImageUrl = "https://images-na.ssl-images-amazon.com/images/I/81TvZqEcZ0L.jpg", ReleaseYear = "2001" },
        };

        context.Books.AddRange(books);
        context.SaveChanges();
    }

    private static void SeedVideoGames(AppDbContext context)
    {
        if (context.Games.Any()) return; // Already seeded

        var games = new List<VideoGame>
        {
            // Nintendo Classics
            new() { Id = 1, Title = "The Legend of Zelda: Breath of the Wild", Developer = "Nintendo", ImageUrl = "https://assets.nintendo.com/image/upload/f_auto/q_auto/dpr_2.0/c_scale,w_600/ncom/en_US/games/switch/t/the-legend-of-zelda-breath-of-the-wild-switch/hero", ReleaseYear = "2017" },
            new() { Id = 2, Title = "Super Mario Odyssey", Developer = "Nintendo", ImageUrl = "https://assets.nintendo.com/image/upload/f_auto/q_auto/dpr_2.0/c_scale,w_600/ncom/en_US/games/switch/s/super-mario-odyssey-switch/hero", ReleaseYear = "2017" },
            new() { Id = 3, Title = "The Legend of Zelda: Tears of the Kingdom", Developer = "Nintendo", ImageUrl = "https://assets.nintendo.com/image/upload/f_auto/q_auto/dpr_2.0/c_scale,w_600/ncom/en_US/games/switch/t/the-legend-of-zelda-tears-of-the-kingdom-switch/hero", ReleaseYear = "2023" },
            new() { Id = 4, Title = "Super Mario 64", Developer = "Nintendo", ImageUrl = "https://assets.nintendo.com/image/upload/f_auto/q_auto/dpr_2.0/c_scale,w_600/ncom/en_US/games/switch/s/super-mario-64-switch/hero", ReleaseYear = "1996" },
            new() { Id = 5, Title = "Metroid Prime", Developer = "Retro Studios", ImageUrl = "https://assets.nintendo.com/image/upload/f_auto/q_auto/dpr_2.0/c_scale,w_600/ncom/en_US/games/switch/m/metroid-prime-remastered-switch/hero", ReleaseYear = "2002" },
            new() { Id = 6, Title = "Animal Crossing: New Horizons", Developer = "Nintendo", ImageUrl = "https://assets.nintendo.com/image/upload/f_auto/q_auto/dpr_2.0/c_scale,w_600/ncom/en_US/games/switch/a/animal-crossing-new-horizons-switch/hero", ReleaseYear = "2020" },

            // PlayStation Exclusives
            new() { Id = 7, Title = "The Last of Us", Developer = "Naughty Dog", ImageUrl = "https://image.api.playstation.com/vulcan/img/rnd/202010/2618/Y3xRGbpP1SlKV5TK9oUGCD8P.png", ReleaseYear = "2013" },
            new() { Id = 8, Title = "God of War", Developer = "Santa Monica Studio", ImageUrl = "https://image.api.playstation.com/vulcan/img/rnd/202010/2217/p3pYq0QxntZQREXRVdAzmn1w.png", ReleaseYear = "2018" },
            new() { Id = 9, Title = "Spider-Man", Developer = "Insomniac Games", ImageUrl = "https://image.api.playstation.com/vulcan/img/rnd/202011/0714/vuF88yWPSnuQTUzdUP84Ws5w.png", ReleaseYear = "2018" },
            new() { Id = 10, Title = "Horizon Zero Dawn", Developer = "Guerrilla Games", ImageUrl = "https://image.api.playstation.com/vulcan/img/rnd/202009/2923/jAT7HjpL9gHPBz3F9FqVxNzK.png", ReleaseYear = "2017" },
            new() { Id = 11, Title = "Bloodborne", Developer = "FromSoftware", ImageUrl = "https://image.api.playstation.com/vulcan/img/rnd/202010/2614/NVmnBXze9ElHzj4w2HSRPZ2x.png", ReleaseYear = "2015" },
            new() { Id = 12, Title = "Ghost of Tsushima", Developer = "Sucker Punch Productions", ImageUrl = "https://image.api.playstation.com/vulcan/img/rnd/202010/0823/V0R1Ps0P5EpRZuIPLUgwcUw6.png", ReleaseYear = "2020" },

            // Xbox Exclusives
            new() { Id = 13, Title = "Halo: Combat Evolved", Developer = "Bungie", ImageUrl = "https://upload.wikimedia.org/wikipedia/en/8/80/Halo_-_Combat_Evolved_%28XBox_version_-_box_art%29.jpg", ReleaseYear = "2001" },
            new() { Id = 14, Title = "Halo 3", Developer = "Bungie", ImageUrl = "https://upload.wikimedia.org/wikipedia/en/b/b9/Halo_3_final_boxshot.JPG", ReleaseYear = "2007" },
            new() { Id = 15, Title = "Gears of War", Developer = "Epic Games", ImageUrl = "https://upload.wikimedia.org/wikipedia/en/1/12/Gears_of_War_box_art.jpg", ReleaseYear = "2006" },
            new() { Id = 16, Title = "Forza Horizon 5", Developer = "Playground Games", ImageUrl = "https://upload.wikimedia.org/wikipedia/en/6/64/Forza_Horizon_5_cover_art.png", ReleaseYear = "2021" },

            // Multi-platform AAA
            new() { Id = 17, Title = "The Witcher 3: Wild Hunt", Developer = "CD Projekt Red", ImageUrl = "https://image.api.playstation.com/vulcan/img/rnd/202010/2614/NVmnBXze9ElHzj4w2HSRPZ2x.png", ReleaseYear = "2015" },
            new() { Id = 18, Title = "Red Dead Redemption 2", Developer = "Rockstar Games", ImageUrl = "https://image.api.playstation.com/vulcan/img/rnd/202010/2618/Y3xRGbpP1SlKV5TK9oUGCD8P.png", ReleaseYear = "2018" },
            new() { Id = 19, Title = "Grand Theft Auto V", Developer = "Rockstar Games", ImageUrl = "https://image.api.playstation.com/vulcan/img/rnd/202010/2618/Y3xRGbpP1SlKV5TK9oUGCD8P.png", ReleaseYear = "2013" },
            new() { Id = 20, Title = "Elden Ring", Developer = "FromSoftware", ImageUrl = "https://image.api.playstation.com/vulcan/img/rnd/202108/0410/ISlVY5ylDsYsTNhzETSjUBU3.png", ReleaseYear = "2022" },
            new() { Id = 21, Title = "Dark Souls", Developer = "FromSoftware", ImageUrl = "https://image.api.playstation.com/vulcan/img/rnd/202010/2614/NVmnBXze9ElHzj4w2HSRPZ2x.png", ReleaseYear = "2011" },
            new() { Id = 22, Title = "Skyrim", Developer = "Bethesda Game Studios", ImageUrl = "https://image.api.playstation.com/vulcan/img/rnd/202010/2614/NVmnBXze9ElHzj4w2HSRPZ2x.png", ReleaseYear = "2011" },
            new() { Id = 23, Title = "Fallout 4", Developer = "Bethesda Game Studios", ImageUrl = "https://image.api.playstation.com/vulcan/img/rnd/202010/2614/NVmnBXze9ElHzj4w2HSRPZ2x.png", ReleaseYear = "2015" },
            new() { Id = 24, Title = "Cyberpunk 2077", Developer = "CD Projekt Red", ImageUrl = "https://image.api.playstation.com/vulcan/img/rnd/202010/2614/NVmnBXze9ElHzj4w2HSRPZ2x.png", ReleaseYear = "2020" },
            new() { Id = 25, Title = "Minecraft", Developer = "Mojang Studios", ImageUrl = "https://image.api.playstation.com/vulcan/img/rnd/202010/2614/NVmnBXze9ElHzj4w2HSRPZ2x.png", ReleaseYear = "2011" },

            // Indie Gems
            new() { Id = 26, Title = "Hollow Knight", Developer = "Team Cherry", ImageUrl = "https://image.api.playstation.com/vulcan/img/rnd/202010/2614/NVmnBXze9ElHzj4w2HSRPZ2x.png", ReleaseYear = "2017" },
            new() { Id = 27, Title = "Celeste", Developer = "Maddy Makes Games", ImageUrl = "https://image.api.playstation.com/vulcan/img/rnd/202010/2614/NVmnBXze9ElHzj4w2HSRPZ2x.png", ReleaseYear = "2018" },
            new() { Id = 28, Title = "Stardew Valley", Developer = "ConcernedApe", ImageUrl = "https://image.api.playstation.com/vulcan/img/rnd/202010/2614/NVmnBXze9ElHzj4w2HSRPZ2x.png", ReleaseYear = "2016" },
            new() { Id = 29, Title = "Hades", Developer = "Supergiant Games", ImageUrl = "https://image.api.playstation.com/vulcan/img/rnd/202010/2614/NVmnBXze9ElHzj4w2HSRPZ2x.png", ReleaseYear = "2020" },
            new() { Id = 30, Title = "Undertale", Developer = "Toby Fox", ImageUrl = "https://image.api.playstation.com/vulcan/img/rnd/202010/2614/NVmnBXze9ElHzj4w2HSRPZ2x.png", ReleaseYear = "2015" },

            // Fighting Games
            new() { Id = 31, Title = "Street Fighter V", Developer = "Capcom", ImageUrl = "https://image.api.playstation.com/vulcan/img/rnd/202010/2614/NVmnBXze9ElHzj4w2HSRPZ2x.png", ReleaseYear = "2016" },
            new() { Id = 32, Title = "Mortal Kombat 11", Developer = "NetherRealm Studios", ImageUrl = "https://image.api.playstation.com/vulcan/img/rnd/202010/2614/NVmnBXze9ElHzj4w2HSRPZ2x.png", ReleaseYear = "2019" },
            new() { Id = 33, Title = "Super Smash Bros. Ultimate", Developer = "Nintendo", ImageUrl = "https://assets.nintendo.com/image/upload/f_auto/q_auto/dpr_2.0/c_scale,w_600/ncom/en_US/games/switch/s/super-smash-bros-ultimate-switch/hero", ReleaseYear = "2018" },

            // Racing
            new() { Id = 34, Title = "Mario Kart 8 Deluxe", Developer = "Nintendo", ImageUrl = "https://assets.nintendo.com/image/upload/f_auto/q_auto/dpr_2.0/c_scale,w_600/ncom/en_US/games/switch/m/mario-kart-8-deluxe-switch/hero", ReleaseYear = "2017" },
            new() { Id = 35, Title = "Gran Turismo 7", Developer = "Polyphony Digital", ImageUrl = "https://image.api.playstation.com/vulcan/img/rnd/202010/2614/NVmnBXze9ElHzj4w2HSRPZ2x.png", ReleaseYear = "2022" },
        };

        context.Games.AddRange(games);
        context.SaveChanges();
    }
}
