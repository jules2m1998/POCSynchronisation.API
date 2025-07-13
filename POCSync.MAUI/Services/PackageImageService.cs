using Poc.Synchronisation.Domain.Abstractions.Repositories;
using Poc.Synchronisation.Domain.Models;
using POCSync.MAUI.Services.Abstractions;

namespace POCSync.MAUI.Services;

public class PackageImageService(IPackagerDocumentRepository repository) : IPackageImageService
{
    public async Task ApplyPackageImage(Guid packageId, string[] imagesPaths)
    {
        var documents = await repository.GetByPackageIdAsync(packageId);
        var intersection = documents
            .Select(doc => doc.Document.StorageUrl)
            .Where(x => x != null)
            .Intersect(imagesPaths).ToList();
        var documentsToDelete = documents
            .Where(doc => !intersection.Contains(doc.Document.StorageUrl))
            .ToList();

        var documentsToAdd = imagesPaths
            .Where(path => !documents.Any(doc => doc.Document.StorageUrl == path))
            .ToList();

        foreach (var doc in documentsToDelete)
        {
            await repository.DeleteAsync(doc);
        }
        foreach (var path in documentsToAdd)
        {
            var document = new Document { StorageUrl = path, FileName = path.Split(Path.PathSeparator).Last(), ModifiedAt = DateTime.UtcNow };
            var packageDocument = new PackageDocument { PackageId = packageId, Document = document };
            await repository.AddAsync(packageDocument);
        }
    }

    public async Task CleanPackageImages(Guid packageId)
    {
        await repository.DeleteByIdAsync(packageId);
    }

    public Task<IReadOnlyCollection<PackageDocument>> GetByPackageIdAsync(Guid packageId)
    {
        return repository.GetByPackageIdAsync(packageId);
    }
}
