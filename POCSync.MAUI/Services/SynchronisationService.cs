using Poc.Synchronisation.Domain.Abstractions.Repositories;

namespace POCSync.MAUI.Services;

public record SynchronisationInfo(
    int TotalEvents,
    int TotalDocumentsToSync
);

public class SynchronisationService(ISynchronisationRepository repository)
{
    public async Task<SynchronisationInfo> GetSynchronisationInfoAsync(CancellationToken cancellationToken = default)
    {
        var eventCount = await repository.GetCountOfEventsToSynchronise();
        var documentCount = await repository.GetCountOfDocumentsToSync();
        return new(eventCount, documentCount);
    }
}
