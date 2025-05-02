using Dapper;
using Infrastructure.Dapper.Abstractions;
using Poc.Synchronisation.Domain;

namespace Infrastructure.Dapper.Repository;

public class StoredEventRepository : IBaserepositoryWithInitialisation<StoredEvent, Guid>
{
    private readonly IDbConnectionFactory _dbConnectionFactory;

    public async Task InitializeAsync()
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        var sql = """
            CREATE TABLE IF NOT EXISTS Events (
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
            CREATE INDEX IF NOT EXISTS idx_events_element_id ON Events(ElementId);
            CREATE INDEX IF NOT EXISTS idx_events_event_type ON Events(EventType);
            CREATE INDEX IF NOT EXISTS idx_events_emited_on ON Events(EmitedOn);
        """;

        var result = await connection.ExecuteAsync(sql);
    }

    public StoredEventRepository(IDbConnectionFactory dbConnectionFactory)
    {
        _dbConnectionFactory = dbConnectionFactory ?? throw new ArgumentNullException(nameof(dbConnectionFactory));
    }

    public async Task<bool> AddAsync(StoredEvent entity, CancellationToken cancellationToken = default)
    {
        // Ensure we have a valid ID
        if (entity.EventId == Guid.Empty)
        {
            entity.EventId = Guid.NewGuid();
        }

        // Set SavedOn to current UTC time
        entity.SavedOn = DateTime.UtcNow;

        const string sql = @"
            INSERT INTO Events (
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

    public async Task<bool> DeleteAsync(StoredEvent entity, CancellationToken cancellationToken = default)
    {
        return await DeleteByIdAsync(entity.EventId, cancellationToken);
    }

    public async Task<bool> DeleteByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM Events WHERE EventId = @EventId;";

        using var connection = _dbConnectionFactory.CreateConnection();
        var affected = await connection.ExecuteAsync(sql, new { EventId = id });

        return affected > 0;
    }

    public async Task<IEnumerable<StoredEvent>> GetAllAsync(bool excludeDeleted = true, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM Events;";

        using var connection = _dbConnectionFactory.CreateConnection();
        var events = await connection.QueryAsync<StoredEvent>(sql);

        foreach (var storedEvent in events)
        {
            if (DateTime.TryParse(storedEvent.EmitedOn.ToString(), out var emitedOn))
                storedEvent.EmitedOn = emitedOn;

            if (DateTime.TryParse(storedEvent.SavedOn.ToString(), out var savedOn))
                storedEvent.SavedOn = savedOn;
        }

        return events;
    }

    public async Task<StoredEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM Events WHERE EventId = @EventId;";

        using var connection = _dbConnectionFactory.CreateConnection();
        var storedEvent = await connection.QuerySingleOrDefaultAsync<StoredEvent>(sql, new { EventId = id });

        if (storedEvent != null)
        {
            if (DateTime.TryParse(storedEvent.EmitedOn.ToString(), out var emitedOn))
                storedEvent.EmitedOn = emitedOn;

            if (DateTime.TryParse(storedEvent.SavedOn.ToString(), out var savedOn))
                storedEvent.SavedOn = savedOn;
        }

        return storedEvent;
    }

    public async Task<IEnumerable<StoredEvent>> GetByElementIdAsync(Guid elementId, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM Events WHERE ElementId = @ElementId ORDER BY EmitedOn DESC;";

        using var connection = _dbConnectionFactory.CreateConnection();
        var events = await connection.QueryAsync<StoredEvent>(sql, new { ElementId = elementId });

        foreach (var storedEvent in events)
        {
            if (DateTime.TryParse(storedEvent.EmitedOn.ToString(), out var emitedOn))
                storedEvent.EmitedOn = emitedOn;

            if (DateTime.TryParse(storedEvent.SavedOn.ToString(), out var savedOn))
                storedEvent.SavedOn = savedOn;
        }

        return events;
    }

    public async Task<IEnumerable<StoredEvent>> GetByEventTypeAsync(string eventType, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM Events WHERE EventType = @EventType ORDER BY EmitedOn DESC;";

        using var connection = _dbConnectionFactory.CreateConnection();
        var events = await connection.QueryAsync<StoredEvent>(sql, new { EventType = eventType });

        foreach (var storedEvent in events)
        {
            if (DateTime.TryParse(storedEvent.EmitedOn.ToString(), out var emitedOn))
                storedEvent.EmitedOn = emitedOn;

            if (DateTime.TryParse(storedEvent.SavedOn.ToString(), out var savedOn))
                storedEvent.SavedOn = savedOn;
        }

        return events;
    }

    public async Task<IEnumerable<StoredEvent>> GetEventsSinceAsync(DateTime since, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM Events WHERE EmitedOn >= @Since ORDER BY EmitedOn;";

        using var connection = _dbConnectionFactory.CreateConnection();
        var events = await connection.QueryAsync<StoredEvent>(sql, new { Since = since.ToString("o") });

        foreach (var storedEvent in events)
        {
            if (DateTime.TryParse(storedEvent.EmitedOn.ToString(), out var emitedOn))
                storedEvent.EmitedOn = emitedOn;

            if (DateTime.TryParse(storedEvent.SavedOn.ToString(), out var savedOn))
                storedEvent.SavedOn = savedOn;
        }

        return events;
    }

    public IQueryable<StoredEvent> Queryable()
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        var events = connection.Query<StoredEvent>("SELECT * FROM Events;");

        foreach (var storedEvent in events)
        {
            if (DateTime.TryParse(storedEvent.EmitedOn.ToString(), out var emitedOn))
                storedEvent.EmitedOn = emitedOn;

            if (DateTime.TryParse(storedEvent.SavedOn.ToString(), out var savedOn))
                storedEvent.SavedOn = savedOn;
        }

        return events.AsQueryable();
    }

    public async Task<bool> UpdateAsync(StoredEvent entity, CancellationToken cancellationToken = default)
    {
        entity.SavedOn = DateTime.UtcNow;

        const string sql = @"
            UPDATE Events
            SET MobileEventId = @MobileEventId,
                ElementId = @ElementId,
                EventStatus = @EventStatus,
                EmitedOn = @EmitedOn,
                SavedOn = @SavedOn,
                LastSyncEvent = @LastSyncEvent,
                EventType = @EventType,
                DataType = @DataType,
                DataJson = @DataJson,
                ConflictWithJson = @ConflictWithJson
            WHERE EventId = @EventId;";

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