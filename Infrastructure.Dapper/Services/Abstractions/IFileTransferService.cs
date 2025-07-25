using System;

namespace Infrastructure.Dapper.Services.Abstractions;

public interface IFileTransferService
{
    Task<(string[] success, string[] failure)> UploadFiles();
    Task<string[]> DownloadFiles();
}
