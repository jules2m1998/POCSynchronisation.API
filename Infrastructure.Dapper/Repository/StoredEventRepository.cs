using Dapper;
using Dommel;
using Poc.Synchronisation.Domain.Events.Packages;

namespace Infrastructure.Dapper.Repository;

public class StoredEventRepository(IDbConnectionFactory dbConnectionFactory) :
    BaserRepository<StoredEvent, Guid>(dbConnectionFactory),
    IBaserepositoryWithInitialisation<StoredEvent, Guid>
{
    public async Task InitializeAsync()
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        var sql = """
            CREATE TABLE IF NOT EXISTS StoredEvents (
                EventId TEXT NOT NULL PRIMARY KEY,
                MobileEventId TEXT NOT NULL,
                UserId TEXT NOT NULL,
                ElementId TEXT NOT NULL,
                EventStatus INTEGER NOT NULL,
                EmitedOn TEXT NOT NULL,
                SavedOn TEXT NOT NULL,
                EventType TEXT NOT NULL,
                DataType TEXT,
                DataJson TEXT NOT NULL,
                ConflictWithJson TEXT NOT NULL
            );
            
            -- Create indexes for better performance
            CREATE INDEX IF NOT EXISTS idx_events_element_id ON StoredEvents(ElementId);
            CREATE INDEX IF NOT EXISTS idx_events_event_type ON StoredEvents(EventType);
            CREATE INDEX IF NOT EXISTS idx_events_emited_on ON StoredEvents(EmitedOn);
        """;

        var result = await connection.ExecuteAsync(sql);
    }

    public override async Task<bool> AddAsync(StoredEvent entity, CancellationToken cancellationToken = default)
    {
        try
        {
            // Ensure we have a valid ID
            if (entity.EventId == Guid.Empty)
            {
                entity.EventId = Guid.NewGuid();
            }
            entity.EventStatus = EventType.Idle;
            entity.EmitedOn = DateTime.UtcNow;

            // Set SavedOn to current UTC time
            entity.SavedOn = DateTime.UtcNow;

            const string sql = @"
            INSERT INTO StoredEvents (
                EventId, MobileEventId, ElementId, EventStatus, EmitedOn, SavedOn, UserId,
                EventType, DataType, DataJson, ConflictWithJson
            )
            VALUES (
                @EventId, @MobileEventId, @ElementId, @EventStatus, @EmitedOn, @SavedOn, @UserId,
                @EventType, @DataType, @DataJson, @ConflictWithJson
            );";

            using var connection = _dbConnectionFactory.CreateConnection();

            var users = await connection.GetAllAsync<User>();
            var userId = users.FirstOrDefault()?.Id ?? Guid.Empty;

            var affected = await connection.ExecuteAsync(sql, new
            {
                entity.EventId,
                entity.MobileEventId,
                entity.ElementId,
                EventStatus = (int)entity.EventStatus,
                entity.EmitedOn, // ISO 8601 format for SQLite
                entity.SavedOn,   // ISO 8601 format for SQLite
                UserId = userId,
                entity.EventType,
                entity.DataType,
                entity.DataJson,
                entity.ConflictWithJson
            });

            return affected > 0;
        }
        catch (Exception ex)
        {
            return false;
        }
    }

    /// <summary>
    /// Deletes all stored events with the specified MobileEventId
    /// </summary>
    /// <param name="mobileEventId">The mobile event ID to delete</param>
    /// <param name="cancellationToken">Optional cancellation token</param>
    /// <returns>The number of records deleted</returns>
    public override async Task<bool> DeleteByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
        DELETE FROM StoredEvents 
        WHERE MobileEventId = @MobileEventId;";

        using var connection = _dbConnectionFactory.CreateConnection();
        var affected = await connection.ExecuteAsync(sql, new { MobileEventId = id });

        return affected > 0;
    }
}