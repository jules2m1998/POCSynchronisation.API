using Poc.Synchronisation.Application.Features.Packages.Commands.CreatePackage;
using Poc.Synchronisation.Application.Features.Packages.Commands.UpdatePackage;
using Poc.Synchronisation.Domain;
using Poc.Synchronisation.Domain.Abstractions;
using Poc.Synchronisation.Domain.Events.Packages;
using Poc.Synchronisation.Domain.Models;
using POCSync.MAUI.Models;
using POCSync.MAUI.Services.Abstractions;
using POCSync.MAUI.Tools;
using System.Text.Json;

namespace POCSync.MAUI.Services;

public class PackageService(
    IBaseRepository<Package, Guid> repository,
    IBaseRepository<StoredEvent, Guid> eventStore,
    IPackageImageService packageImageService,
    FileManager fileManager
    ) : IPackageService
{
    public async Task<bool> AddPackageAsync(CreatePackageCommand command, string[] images, CancellationToken cancellationToken = default)
    {
        var package = new Package
        {
            Id = Guid.NewGuid(),
            Reference = command.Reference,
            Weight = command.Weight,
            Volume = command.Volume,
            TareWeight = command.TareWeight
        };
        var result = await repository.AddAsync(package);
        if (!result)
        {
            return false;
        }

        var newPackage = await repository.GetByIdAsync(package.Id, cancellationToken: cancellationToken);
        if (newPackage is null)
        {
            return false;
        }
        var @event = CreatePackageEvent.From(newPackage, images);
        _ = eventStore.AddAsync(@event.ToEventStore(), cancellationToken);

        await packageImageService.ApplyPackageImage(package.Id, images);
        return true;
    }

    public async Task<bool> DeletePackageAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await repository.DeleteByIdAsync(id, cancellationToken);
        if (!result)
        {
            return false;
        }
        _ = CreateDeleteEvent(id, cancellationToken);
        await packageImageService.CleanPackageImages(id);
        return true;
    }

    private async Task CreateDeleteEvent(Guid id, CancellationToken cancellationToken)
    {
        var allEvents = eventStore.Queryable().Where(x => x.MobileEventId == id);
        var jsonD = "";
        if (allEvents.Any(x => x.EventType == nameof(CreatePackageEvent)))
        {
            foreach (var item in allEvents)
            {
                await eventStore.DeleteByIdAsync(item.EventId, cancellationToken);
            }

            jsonD = JsonSerializer.Serialize(await eventStore.GetAllAsync());
            return;
        }

        var editEvents = allEvents.Where(x => x.EventType == nameof(UpdatePackageEvent)).ToList();
        foreach (var item in editEvents)
        {
            await eventStore.DeleteByIdAsync(item.EventId, cancellationToken);
        }

        var @event = new DeletePackageEvent
        {
            Data = id,
            MobileEventId = id
        };

        jsonD = JsonSerializer.Serialize(await eventStore.GetAllAsync());
        _ = eventStore.AddAsync(@event.ToEventStore(), cancellationToken);
    }

    public async Task<IEnumerable<PackageModel>> GetAllPackagesAsync(CancellationToken cancellationToken = default)
    {
        var result = await repository.GetAllAsync(cancellationToken: cancellationToken);
        ICollection<PackageModel> models = []; ;
        foreach (var package in result)
        {
            var model = await MapToPackageModel(package);
            models.Add(model);
        }
        return models;
    }

    public async Task<Package?> GetPackageByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await repository.GetByIdAsync(id, cancellationToken: cancellationToken);
        return result;
    }

    public async Task<bool> UpdatePackageAsync(UpdatePackageCommand command, string[] images, CancellationToken cancellationToken = default)
    {
        var package = await repository.GetByIdAsync(command.Id, cancellationToken: cancellationToken);
        if (package is null)
        {
            return false;
        }
        package.Reference = command.Reference;
        package.Weight = command.Weight;
        package.Volume = command.Volume;
        package.TareWeight = command.TareWeight;
        var result = await repository.UpdateAsync(package, cancellationToken);

        if (!result)
        {
            return false;
        }
        var newPackage = await repository.GetByIdAsync(package.Id, cancellationToken: cancellationToken);

        if (newPackage is null)
        {
            return false;
        }
        _ = CreateUpdateEvent(package, newPackage, cancellationToken, images);
        await packageImageService.ApplyPackageImage(package.Id, images);

        return true;
    }

    private async Task CreateUpdateEvent(Package package, Package newPackage, CancellationToken cancellationToken, string[] images)
    {
        var @createEvent = UpdatePackageEvent.From(package, newPackage, images);
        var storedEvent = @createEvent.ToEventStore();
        storedEvent.EventId = Guid.NewGuid();

        var result = await eventStore.AddAsync(storedEvent, cancellationToken);
        if (!result) return;
        var similars = eventStore
            .Queryable()
            .Where(
                x => x.MobileEventId == storedEvent.MobileEventId
                    && x.EventType == nameof(UpdatePackageEvent)
                    && x.EventId != storedEvent.EventId
                )
            .ToList();
        foreach (var item in similars)
        {
            await eventStore.DeleteByIdAsync(item.EventId, cancellationToken);
        }
    }

    private async Task<PackageModel> MapToPackageModel(Package package)
    {
        var documents = await packageImageService.GetByPackageIdAsync(package.Id);
        var images = documents.Select(doc => fileManager.GetImageSourceFromPath(doc.Document.StorageUrl)).Where(x => x != null).ToList();
        return new PackageModel
        {
            Id = package.Id,
            Reference = package.Reference,
            Weight = package.Weight,
            Volume = package.Volume,
            TareWeight = package.TareWeight,
            Images = images.Where(x => x != null).ToList()!,
            IsConflicted = package.ConflictOfId.HasValue,
        };
    }
}
