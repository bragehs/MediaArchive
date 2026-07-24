using MediaArchive.Data;
using Microsoft.EntityFrameworkCore;

namespace MediaArchive.Services;

public record OnDeckItem(int UserMediaItemId, string Title);

public class CollectionService(IDbContextFactory<AppDbContext> dbContextFactory)
{
    public Task<List<OnDeckItem>> GetOnDeckAsync()
    {
        throw new NotImplementedException();
    }
}