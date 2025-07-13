namespace Infrastructure.Dapper.Services;

using Infrastructure.Dapper.Services.Abstractions;
using Microsoft.Extensions.Logging;
using Poc.Synchronisation.Domain.Abstractions.Services;  // Adjust to your project's namespace
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

public class DocumentService(ILogger<DocumentService> logger, IFileSystemPath systemPath) : IDocumentService
{
    private readonly ILogger<DocumentService> _logger = logger;
    private readonly string _basePath = Path.Combine(systemPath.BasePath(), "wasteflow-images");

    public async Task<string> CreateDocumentAsync(Stream stream, string fileName, string[] treeFolder)
    {
        ValidateInput(stream, fileName);

        // Build a relative path (folders + filename)
        var relativePath = BuildRelativePath(fileName, treeFolder);
        var fullPath = Path.Combine(_basePath, relativePath);
        var directory = Path.GetDirectoryName(fullPath);

        if (!Directory.Exists(_basePath))
            Directory.CreateDirectory(_basePath!);

        // Ensure directory exists
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory!);

        // Write the incoming stream to file
        if (stream.CanSeek)
            stream.Position = 0;

        await using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
        await stream.CopyToAsync(fileStream);

        _logger.LogInformation("Document created: {FullPath}", fullPath);
        return relativePath;
    }

    public Task<bool> DeleteDocumentAsync(string relativePath)
    {
        var fullPath = Path.Combine(_basePath, relativePath);
        if (string.IsNullOrWhiteSpace(relativePath) || !File.Exists(fullPath))
            return Task.FromResult(false);

        try
        {
            File.Delete(fullPath);
            _logger.LogInformation("Document deleted: {FullPath}", fullPath);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting document: {FullPath}", fullPath);
            return Task.FromResult(false);
        }
    }

    #region Helpers

    private static void ValidateInput(Stream stream, string fileName)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name cannot be empty.", nameof(fileName));
    }

    private static string BuildRelativePath(string fileName, string[] treeFolder)
    {
        // Sanitize file name
        var safeName = SanitizeFileName(fileName);

        // Optionally generate a unique name: uncomment if needed
        // safeName = GenerateUniqueFileName(safeName);

        // Sanitize and combine folders
        var parts = (treeFolder ?? [])
            .Select(f => SanitizeFolderName(f))
            .Where(f => !string.IsNullOrEmpty(f))
            .ToList();
        parts.Add(safeName);

        return Path.Combine([.. parts]);
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string([.. fileName.Where(c => !invalid.Contains(c))]);
        return string.IsNullOrWhiteSpace(cleaned) ? Guid.NewGuid().ToString() : cleaned;
    }

    private static string SanitizeFolderName(string folderName)
    {
        var invalid = Path.GetInvalidPathChars().Concat(['/', '\\']).ToArray();
        var cleaned = new string([.. folderName.Where(c => !invalid.Contains(c))]);
        return string.IsNullOrWhiteSpace(cleaned) ? string.Empty : cleaned;
    }

    private static string GenerateUniqueFileName(string fileName)
    {
        var ext = Path.GetExtension(fileName);
        var name = Path.GetFileNameWithoutExtension(fileName);
        return $"{name}_{Guid.NewGuid():N}{ext}";
    }

    public string GetBaseUrl() => _basePath;

    #endregion
}
