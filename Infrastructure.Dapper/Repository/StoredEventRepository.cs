using Dapper;
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
                ElementId TEXT NOT NULL,
                EventStatus INTEGER NOT NULL,
                EmitedOn TEXT NOT NULL,
                SavedOn TEXT NOT NULL,
                LastSyncEvent TEXT NOT NULL,
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
        // Ensure we have a valid ID
        if (entity.EventId == Guid.Empty)
        {
            entity.EventId = Guid.NewGuid();
        }
        entity.MobileEventId = Guid.NewGuid();
        entity.EventStatus = EventType.Idle;
        entity.EmitedOn = DateTime.UtcNow;

        // Set SavedOn to current UTC time
        entity.SavedOn = DateTime.UtcNow;

        const string sql = @"
            INSERT INTO StoredEvents (
                EventId, MobileEventId, ElementId, EventStatus, EmitedOn, SavedOn, 
                LastSyncEvent, EventType, DataType, DataJson, ConflictWithJson
            )
            VALUES (
                @EventId, @MobileEventId, @ElementId, @EventStatus, @EmitedOn, @SavedOn, 
                @LastSyncEvent, @EventType, @DataType, @DataJson, @ConflictWithJson
            );";

        using var connection = _dbConnectionFactory.CreateConnection();
        var affected = await connection.ExecuteAsync(sql, new
        {
            entity.EventId,
            entity.MobileEventId,
            entity.ElementId,
            EventStatus = (int)entity.EventStatus,
            EmitedOn = entity.EmitedOn.ToString("o"), // ISO 8601 format for SQLite
            SavedOn = entity.SavedOn.ToString("o"),   // ISO 8601 format for SQLite
            entity.LastSyncEvent,
            entity.EventType,
            entity.DataType,
            entity.DataJson,
            entity.ConflictWithJson
        });

        return affected > 0;
    }
}