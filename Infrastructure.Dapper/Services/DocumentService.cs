using Dapper;
using Infrastructure.Dapper.Services.Generated;
using Microsoft.Extensions.Logging;
using Poc.Synchronisation.Domain.Abstractions.Services;
using Poc.Synchronisation.Domain.Types;
using System.Data;
using System.Net;
using System.Runtime.CompilerServices;

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

    //public async IAsyncEnumerable<(string description, double progress, bool isNewStep, string? stepTitle, int? total)> UploadFiles()
    public async IAsyncEnumerable<UploadFilesStepModel> UploadFiles([EnumeratorCancellation] CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        yield return UploadFilesStepModel.CreateSuccess("Initialisation de la récupération des fichiers à uploader...", 0.0, true, "Récupération des fichiers", null);

        cancellation.ThrowIfCancellationRequested();
        var connection = dbConnectionFactory.CreateConnection();
        var nonSyncedFiles = (await connection.QueryAsync<string>(sqlGetAllNonSyncFilePaths)).ToArray();
        var totalFiles = nonSyncedFiles.Length;

        if (totalFiles == 0)
        {
            yield return UploadFilesStepModel.CreateSuccess("Aucun fichier à uploader. Étape terminée ✅", 1.0, false, null, 0);
            yield break;
        }

        yield return UploadFilesStepModel.CreateSuccess($"📁 {totalFiles} fichier(s) à uploader récupéré(s).", 1.0, false, null, totalFiles);
        yield return UploadFilesStepModel.CreateSuccess("Début de l'upload des fichiers vers le serveur...", 0.0, true, "Upload des fichiers", null);

        var failure = new List<string>();
        var success = new List<string>();

        int index = 0;
        foreach (var url in nonSyncedFiles)
        {
            cancellation.ThrowIfCancellationRequested();

            var path = Path.Combine(_basePath, url);
            if (!File.Exists(path))
            {
                _logger.LogWarning("Fichier introuvable : {Path}", path);
                failure.Add(url);
                yield return UploadFilesStepModel.CreateError("", $"❌ Fichier introuvable : {url}", (double)index / totalFiles);
                index++;
                continue;
            }

            using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
            var streamPart = new StreamPart(fileStream, Path.GetFileName(path), "application/octet-stream");

            var folders = Path.GetDirectoryName(url)?.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries) ?? [];

            var result = await api.Upload(streamPart, folders);
            if (!result.Success)
            {
                failure.Add(url);
                yield return UploadFilesStepModel.CreateError("", $"❌ Échec de l'upload : {Path.GetFileName(path)}", (double)index / totalFiles);
                index++;
                continue;
            }

            var update = await connection.ExecuteAsync(sqlUpdateDocumentSyncStatus, new { StorageUrl = url });
            if (update == 0)
            {
                _logger.LogWarning("Échec de la mise à jour : {Path}", path);
                failure.Add(url);
                yield return UploadFilesStepModel.CreateError("", $"❌ Upload réussi mais mise à jour échouée : {Path.GetFileName(path)}", (double)index / totalFiles);
            }
            else
            {
                success.Add(url);
                yield return UploadFilesStepModel.CreateSuccess($"✅ {Path.GetFileName(path)} uploadé avec succès", (double)index / totalFiles, false, null, null, 1);
            }

            index++;
        }

        yield return UploadFilesStepModel.CreateSuccess($"📦 Upload terminé. {success.Count} succès, {failure.Count} échecs.", 1.0, false, null, null);
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

    public async IAsyncEnumerable<DownloadFilesStep> DownloadFiles([EnumeratorCancellation] CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();

        var permissionGranted = await permissionManger.CheckAndRequestStoragePermission();
        if (!permissionGranted)
        {
            yield return DownloadFilesStep.CreateError(
                "Action non autorisée 🚫",
                "Vous n'avez pas les droits nécessaires pour cette action.",
                1.0);
            yield break;
        }

        cancellation.ThrowIfCancellationRequested();

        string[] nonSyncedFiles = [];
        IDbConnection connection;
        Exception? queryException = null;

        try
        {
            connection = dbConnectionFactory.CreateConnection();
            nonSyncedFiles = (await connection.QueryAsync<string>(sqlGetNonUserFilePaths)).ToArray();
        }
        catch (Exception ex)
        {
            queryException = ex;
            connection = null!;
        }

        if (queryException is not null)
        {
            var msg = $"{queryException.GetType().Name} (0x{queryException.HResult:X}): {queryException.Message}";
            _logger.LogError(queryException, "Erreur lors de la récupération des fichiers non synchronisés");
            yield return DownloadFilesStep.CreateError("Erreur de récupération", msg, 1.0);
            yield break;
        }

        cancellation.ThrowIfCancellationRequested();

        if (nonSyncedFiles.Length == 0)
        {
            yield return DownloadFilesStep.CreateSuccess("Aucun fichier à télécharger 📂", 1.0);
            yield break;
        }

        yield return DownloadFilesStep.CreateSuccess($"{nonSyncedFiles.Length} fichier(s) à synchroniser 📁", 0.1, total: nonSyncedFiles.Length);

        double progress = 0.1;
        double step = 0.9 / nonSyncedFiles.Length;

        foreach (var path in nonSyncedFiles)
        {
            cancellation.ThrowIfCancellationRequested();

            string fileName = Path.GetFileName(path);
            string fullPath = Path.Combine(_basePath, path);
            string? directory = Path.GetDirectoryName(fullPath);

            bool hadError = false;
            string? errorTitle = null;
            string? errorDetail = null;

            try
            {
                using var response = await fileClient.GetAsync(path, cancellation);

                cancellation.ThrowIfCancellationRequested();

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    hadError = true;
                    errorDetail = $"Fichier introuvable sur le serveur: {fileName}";
                    errorTitle = "Le fichier n'existe pas sur le serveur (404).";
                    _logger.LogWarning("Fichier 404: {Path}", path);
                }
                else
                {
                    response.EnsureSuccessStatusCode();

                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                        Directory.CreateDirectory(directory);

                    using var stream = File.OpenWrite(fullPath);
                    await response.Content.CopyToAsync(stream, cancellation);

                    cancellation.ThrowIfCancellationRequested();

                    var updated = await connection.ExecuteAsync(
                        sqlUpdateDocumentSyncStatus,
                        new { StorageUrl = path }
                    );

                    if (updated == 0)
                    {
                        hadError = true;
                        errorTitle = $"Sync échouée: {fileName}";
                        errorDetail = "La base de données n'a pas été mise à jour.";
                        _logger.LogWarning("Aucune ligne mise à jour pour {Path}", path);
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                hadError = true;
                errorTitle = $"Erreur HTTP: {fileName}";
                errorDetail = $"{ex.GetType().Name} (0x{ex.HResult:X}): {ex.Message}";
                _logger.LogError(ex, "Erreur HTTP pour {Path}", path);
            }
            catch (Exception ex)
            {
                hadError = true;
                errorTitle = $"Erreur: {fileName}";
                errorDetail = $"{ex.GetType().Name} (0x{ex.HResult:X}): {ex.Message}";
                _logger.LogError(ex, "Erreur pour {Path}", path);
            }

            progress += step;

            if (hadError)
            {
                yield return DownloadFilesStep.CreateError(errorTitle!, errorDetail!, Math.Min(progress, 0.99));
            }
            else
            {
                yield return DownloadFilesStep.CreateSuccess($"✅ Téléchargé: {fileName}", Math.Min(progress, 0.99), syncedCount: 1);
            }
        }

        cancellation.ThrowIfCancellationRequested();

        yield return DownloadFilesStep.CreateSuccess("📥 Téléchargement terminé", 1.0);
    }

    #endregion
}
