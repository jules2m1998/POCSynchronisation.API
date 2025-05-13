using Poc.Synchronisation.Application.Features.Packages.Commands.CreatePackage;
using Poc.Synchronisation.Application.Features.Packages.Commands.UpdatePackage;
using Poc.Synchronisation.Domain.Models;

namespace POCSync.MAUI.Services.Abstractions;

public interface IPackageService
{
    Task<bool> AddPackageAsync(CreatePackageCommand package, CancellationToken cancellationToken = default);
    Task<bool> DeletePackageAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> UpdatePackageAsync(UpdatePackageCommand package, CancellationToken cancellationToken = default);
    Task<IEnumerable<Package>> GetAllPackagesAsync(CancellationToken cancellationToken = default);
    Task<Package?> GetPackageByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
