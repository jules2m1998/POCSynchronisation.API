using Poc.Synchronisation.Application.Features.Packages.Commands.CreatePackage;
using Poc.Synchronisation.Application.Features.Packages.Commands.UpdatePackage;
using Poc.Synchronisation.Domain.Models;
using POCSync.MAUI.Models;

namespace POCSync.MAUI.Services.Abstractions;

public interface IPackageService
{
    Task<bool> AddPackageAsync(CreatePackageCommand package, string[] images, CancellationToken cancellationToken = default);
    Task<bool> DeletePackageAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> UpdatePackageAsync(UpdatePackageCommand package, string[] images, CancellationToken cancellationToken = default);
    Task<IEnumerable<PackageModel>> GetAllPackagesAsync(CancellationToken cancellationToken = default);
    Task<Package?> GetPackageByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
