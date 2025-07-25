using Dapper;
using Dommel;
using Microsoft.Extensions.Logging;
using Poc.Synchronisation.Domain.Events.Packages;

namespace Infrastructure.Dapper.Repository;

public class StoredEventRepository(IDbConnectionFactory dbConnectionFactory, ILogger<StoredEventRepository> logger) :
    BaserRepository<StoredEvent, Guid>(dbConnectionFactory),
    IBaserepositoryWithInitialisation<StoredEvent, Guid>
{
    public async Task InitializeAsync()
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        await connection.ExecuteAsync("PRAGMA foreign_keys = ON;");
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
                ConflictWithJson TEXT NOT NULL,
                Metadata TEXT
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
                EventType, DataType, DataJson, ConflictWithJson, Metadata
            )
            VALUES (
                @EventId, @MobileEventId, @ElementId, @EventStatus, @EmitedOn, @SavedOn, @UserId,
                @EventType, @DataType, @DataJson, @ConflictWithJson, @Metadata
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
                entity.ConflictWithJson,
                Metadata = entity.Metadata ?? string.Empty
            });

            return affected > 0;
        }
        catch (Exception)
        {
            return false;
        }
    }
}