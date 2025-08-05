using Poc.Synchronisation.Domain.Abstractions.Services;
using Poc.Synchronisation.Domain.Events.Packages;
using static Dapper.SqlMapper;

namespace Infrastructure.Dapper.Services.EventIdUpdaters;

public class CreatePackageIdUpdater(IDbConnectionFactory dbConnectionFactory, IDBForeignKeyMode dBForeignKey) : IEventIdUpdater
{
    public async Task<IReadOnlyCollection<StoredEvent>> UpdateEventId(IReadOnlyCollection<StoredEvent> events)
    {
        await dBForeignKey.On();
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

            await connection.ExecuteAsync("UPDATE PackageDocuments SET PackageId = @newPackageId WHERE PackageId = @oldPackageId",
            new { newPackageId = item.ElementId, oldPackageId = item.MobileEventId });
            result.Add(item);
        }

        await dBForeignKey.Off();

        return result;
    }
}
