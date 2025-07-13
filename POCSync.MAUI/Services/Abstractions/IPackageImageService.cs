using Poc.Synchronisation.Domain.Models;

namespace POCSync.MAUI.Services.Abstractions;

public interface IPackageImageService
{
    Task ApplyPackageImage(Guid packageId, string[] imagesPaths);
    Task CleanPackageImages(Guid packageId);
    Task<IReadOnlyCollection<PackageDocument>> GetByPackageIdAsync(Guid packageId);
}
