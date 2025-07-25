using Dapper;
using Infrastructure.Dapper.Services.Abstractions;
using Infrastructure.Dapper.Services.Generated;
using Microsoft.Extensions.Logging;
using Poc.Synchronisation.Domain.Abstractions.Services;

namespace Infrastructure.Dapper.Services;

public class DocumentService(
    ILogger<DocumentService> logger,
    IFileSystemPath systemPath,
    IDbConnectionFactory dbConnectionFactory,
    IApi api,
    IHttpClientFactory clientBuilder,
    IPermissionManger permissionManger
) : IDocumentService, IFileTransferService
{
    const string sqlGetAllNonSyncFilePaths = """
        SELECT StorageUrl
        FROM Documents
        WHERE IsSynced = 0
        AND FileName LIKE (
            SELECT REPLACE(Name, ' ', '_') || '%'
            FROM Users
            LIMIT 1
        );
    """;

    const string sqlGetNonUserFilePaths = @"
        SELECT StorageUrl
        FROM Documents
        WHERE IsSynced = 0
        AND FileName NOT LIKE (
            SELECT REPLACE(Name, ' ', '_') || '%'
            FROM Users
            LIMIT 1
        );
    ";

    const string sqlUpdateDocumentSyncStatus = """
        UPDATE Documents
        SET IsSynced = 1
        WHERE StorageUrl = @StorageUrl;
    """;

    const string sqlGetUserId = """
        SELECT REPLACE(Name, ' ', '_') AS Name FROM Users LIMIT 1
    """;

    private readonly ILogger<DocumentService> _logger = logger;
    private readonly string _basePath = Path.Combine(systemPath.BasePath(), "wasteflow-images");
    private readonly HttpClient fileClient = clientBuilder.CreateClient("FileDownloadClient");

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

    private string BuildRelativePath(string fileName, string[] treeFolder)
    {
        // Sanitize file name
        var safeName = SanitizeFileName(fileName);

        // Optionally generate a unique name: uncomment if needed
        // safeName = GenerateUniqueFileName(safeName);

        // Sanitize and combine folders
        var parts = (treeFolder ?? [])
            .Select(SanitizeFolderName)
            .Where(f => !string.IsNullOrEmpty(f))
            .ToList();
        parts.Add(safeName);

        return Path.Combine([.. parts]);
    }

    private string SanitizeFileName(string fileName)
    {
        var shortGuid = Guid.NewGuid().ToString("N")[..8];
        var userName = GetUserNameAsync().Result ?? "unknown_user";
        var extension = Path.GetExtension(fileName);

        return $"{userName}_{shortGuid}{extension}";
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

    public async Task<(string[] success, string[] failure)> UploadFiles()
    {
        try
        {
            var connection = dbConnectionFactory.CreateConnection();
            var nonSyncedFiles = (await connection.QueryAsync<string>(sqlGetAllNonSyncFilePaths)).ToArray();

            var failure = new List<string>();
            var success = new List<string>();
            foreach (var url in nonSyncedFiles)
            {
                var path = Path.Combine(_basePath, url);
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    _logger.LogWarning("File not found or path is empty: {Path}", path);
                    failure.Add(url);
                    continue;
                }
                using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
                var fileName = Path.GetFileName(path);
                var streamPart = new StreamPart(fileStream, fileName, "application/octet-stream");
                var directory = Path.GetDirectoryName(url);
                var folders = directory?.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries) ?? [];
                var result = await api.Upload(streamPart, folders);
                if (!result.Success)
                {
                    failure.Add(url);
                    continue;
                }
                var r = await connection.ExecuteAsync(
                    sqlUpdateDocumentSyncStatus,
                    new { StorageUrl = url }
                );
                if (r == 0)
                {
                    _logger.LogWarning("Failed to update sync status for: {Path}", path);
                    failure.Add(url);
                    continue;
                }
                success.Add(url);
            }

            return (success.ToArray(), failure.ToArray());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during file upload");
            return (Array.Empty<string>(), Array.Empty<string>());
        }
    }

    private async Task<string?> GetUserNameAsync()
    {
        try
        {
            using var connection = dbConnectionFactory.CreateConnection();
            var userName = await connection.QueryFirstOrDefaultAsync<string?>(sqlGetUserId);
            return userName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user ID");
            return string.Empty;
        }
    }

    public async Task<string[]> DownloadFiles()
    {
        var permissionGranted = await permissionManger.CheckAndRequestStoragePermission();
        if (!permissionGranted) return [];

        try
        {
            var result = new List<string>();
            var connection = dbConnectionFactory.CreateConnection();
            var nonSyncedFiles = (await connection.QueryAsync<string>(sqlGetNonUserFilePaths)).ToArray();
            foreach (var path in nonSyncedFiles)
            {
                using var response = await fileClient.GetAsync(path);

                _logger.LogError("Failed to download file from URL: {Url}", response.RequestMessage?.RequestUri);
                response.EnsureSuccessStatusCode();
                var fullPath = Path.Combine(_basePath, path);
                var directory = Path.GetDirectoryName(fullPath);
                if (directory is not null && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                using var stream = File.OpenWrite(fullPath);

                await response.Content.CopyToAsync(stream);
                result.Add(fullPath);
                var _ = await connection.ExecuteAsync(
                    sqlUpdateDocumentSyncStatus,
                    new { StorageUrl = path }
                );
                _logger.LogInformation("File downloaded: {FullPath}", fullPath);
            }
            return [.. result];

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading files");
            throw;
        }
    }
    #endregion
}
