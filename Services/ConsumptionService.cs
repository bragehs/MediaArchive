using MediaArchive.Data;
using Microsoft.EntityFrameworkCore;

namespace MediaArchive.Services;

public record OpenNowItem(
    int UserMediaItemId,
    string Title,
    string? ImageUrl,
    double? Progress, // null when Length is unknown                                                                                                                                                                              
    int DaysOpen,
    int DaysSinceTouched);

public record JustClosedItem(int UserMediaItemId, string Title, int DaysSinceClosed);

public class ConsumptionService(IDbContextFactory<AppDbContext> dbContextFactory)
{
    public Task<List<OpenNowItem>> GetOpenNowAsync()
    {
        throw new NotImplementedException();
    }

    public Task<List<JustClosedItem>> GetJustClosedAsync()
    {
        throw new NotImplementedException();
    }
}