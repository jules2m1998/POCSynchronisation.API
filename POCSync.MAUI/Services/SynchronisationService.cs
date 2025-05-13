using Infrastructure.Dapper.Services.Generated;
using Mapster;
using Poc.Synchronisation.Domain;
using Poc.Synchronisation.Domain.Abstractions;
using POCSync.MAUI.Extensions;

namespace POCSync.MAUI.Services;

public class SynchronisationService(IApi api, IBaseRepository<StoredEvent, Guid> eventRepo)
{
    public async Task<bool> RetrieveAsync()
    {
        try
        {
            // Call the API to synchronise data
            var data = await api.Retrieve();
            if (data == null)
            {
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            // Handle exception
            return false;
        }
    }

    public async Task<bool> SynchroniseAsync()
    {
        try
        {
            var eventStores = await eventRepo.GetAllAsync();
            if (eventStores == null || !eventStores.Any())
            {
                return false;
            }

            foreach (var batch in eventStores.Batch(10))
            {
                var request = new SynchronisationRequest
                {
                    Events = batch.Adapt<ICollection<SynchronisedStoredEventDto>>()
                };
                var result = await api.Synchronisation(request);
            }
            return true;
        }
        catch (Exception ex)
        {
            // Handle exception
            return false;
        }
    }
}
