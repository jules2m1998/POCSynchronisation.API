using Mediator.Abstractions;
using Poc.Synchronisation.Application.Features.Packages.Commands.CreatePackage;
using Poc.Synchronisation.Application.Features.Packages.Commands.DeletePackage;
using Poc.Synchronisation.Application.Features.Packages.Commands.UpdatePackage;
using Poc.Synchronisation.Application.Features.Packages.Queries.GetPackageById;
using Poc.Synchronisation.Application.Features.Packages.Queries.GetPackages;
using Poc.Synchronisation.Domain;
using Poc.Synchronisation.Domain.Abstractions;
using Poc.Synchronisation.Domain.Events.Packages;
using Poc.Synchronisation.Domain.Models;
using POCSync.MAUI.Services.Abstractions;

namespace POCSync.MAUI.Services;

public class PackageService(ISender sender, IBaseRepository<StoredEvent, Guid> eventStore) : IPackageService
{
    public async Task<bool> AddPackageAsync(CreatePackageCommand command, CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(command, cancellationToken);
        if (result == null || result.Id == null)
        {
            return false;
        }
        Guid id = (Guid)result.Id;
        var package = await sender.Send(new GetPackageByIdQuery(id), cancellationToken);
        if (package.Package is null)
        {
            return false;
        }
        var @createEvent = new CreatePackageEvent
        {
            Data = package.Package
        };
        _ = eventStore.AddAsync(@createEvent.ToEventStore(), cancellationToken);
        return true;
    }

    public async Task<bool> DeletePackageAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var deletePackageCommand = new DeletePackageCommand(id);
        var result = await sender.Send(deletePackageCommand, cancellationToken);
        if (!result.Succes)
        {
            return false;
        }
        var @event = new DeletePackageEvent
        {
            Data = id
        };
        _ = eventStore.AddAsync(@event.ToEventStore(), cancellationToken);
        return true;
    }

    public async Task<IEnumerable<Package>> GetAllPackagesAsync(CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(new GetPackagesQuery(), cancellationToken);
        return result.Packages;
    }

    public async Task<Package?> GetPackageByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(new GetPackageByIdQuery(id), cancellationToken);
        return result.Package;
    }

    public async Task<bool> UpdatePackageAsync(UpdatePackageCommand command, CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(command, cancellationToken);
        if (!result.Success)
        {
            return false;
        }
        var package = await sender.Send(new GetPackageByIdQuery(command.Id), cancellationToken);

        if (package.Package is null)
        {
            return false;
        }
        var @createEvent = new UpdatePackageEvent
        {
            Data = package.Package
        };
        _ = eventStore.AddAsync(@createEvent.ToEventStore(), cancellationToken);
        return true;
    }
}
