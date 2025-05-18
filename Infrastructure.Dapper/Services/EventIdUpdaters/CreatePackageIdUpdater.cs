

using Infrastructure.Dapper.Abstractions;
using Poc.Synchronisation.Domain.Events.Packages;
using static Dapper.SqlMapper;

namespace Infrastructure.Dapper.Services.EventIdUpdaters;

public class CreatePackageIdUpdater(IDbConnectionFactory dbConnectionFactory) : IEventIdUpdater
{
    public async Task<IReadOnlyCollection<StoredEvent>> UpdateEventId(IReadOnlyCollection<StoredEvent> events)
    {
        var packageEvents = events.Where(x => x.EventType == nameof(CreatePackageEvent))
            .ToList();
        if (packageEvents.Count == 0) return [];

        var result = new List<StoredEvent>();

        foreach (var item in packageEvents)
        {
            const string sql = """
                UPDATE Packages
                SET Id = @Id
                WHERE Id = @OldId;
            """;

            var parameters = new
            {
                Id = item.ElementId,
                OldId = item.MobileEventId
            };

            using var connection = dbConnectionFactory.CreateConnection();
            var affected = await connection.ExecuteAsync(sql, parameters);

            if (affected == 1)
            {
                item.MobileEventId = item.ElementId;
            }
            result.Add(item);
        }

        return result;
    }
}
