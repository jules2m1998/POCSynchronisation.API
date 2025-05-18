namespace Infrastructure.Dapper.Abstractions;

public interface IEventIdUpdater
{
    public Task<IReadOnlyCollection<StoredEvent>> UpdateEventId(IReadOnlyCollection<StoredEvent> @events);
}
