using Poc.Synchronisation.Domain.Abstractions.Services;
using POCSync.MAUI.Models;

namespace POCSync.MAUI.Tools;

public class FileManager(IDocumentService documentService)
{
    public ImageModel? GetImageSourceFromPath(string? path)
    {
        if (path == null && string.IsNullOrEmpty(path))
            return null;
        var full = Path.Combine(documentService.GetBaseUrl(), path);
        if (!File.Exists(full))
        {
            return null;
        }
        var bites = File.ReadAllBytes(full);
        return new ImageModel
        {
            Source = ImageSource.FromStream(() => new MemoryStream(bites)),
            Path = path
        };
    }
}
