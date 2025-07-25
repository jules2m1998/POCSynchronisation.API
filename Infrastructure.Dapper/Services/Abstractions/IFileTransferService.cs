namespace Infrastructure.Dapper.Services.Abstractions;

public interface IFileTransferService
{
    IAsyncEnumerable<(string description, double progress, bool isNewStep, string? stepTitle, int? total)> UploadFiles();
    IAsyncEnumerable<(string, double)> DownloadFiles();
}
