using Mediator.Abstractions;

namespace Infrastructure.Dapper.DataConflictsReconcilers;

public class PackageConflictReconciler(
    IEmitter emitter
) : IConflictReconciler
{

    public bool CanReconcileConflict(StoredEvent @event) =>
        @event.DataType != null &&
        @event.DataType == typeof(Package).Name &&
        @event.EventConflict is not null &&
        @event.ConflictWithJson != null;

    public async Task ReconciliateAsync(StoredEvent @event, CancellationToken cancellationToken = default)
    {
        var conflictSource = @event.EventConflict;
        var domainEvent = conflictSource!.ToDomainEvent();
        if (domainEvent is null)
        {
            return;
        }
        var result = await emitter.EmitAsync(domainEvent, cancellationToken);
        if (result is null || !result.Any())
        {
            return;
        }
        var conflictDomainEvent = @event.ToDomainEvent();
        if (conflictDomainEvent is null)
        {
            return;
        }
        await emitter.EmitAsync(conflictDomainEvent, cancellationToken);
    }
}
