using Infrastructure.Dapper.Services.Abstractions;

namespace POCSync.MAUI.Services;

public class FileSystemPath : IFileSystemPath
{
    public string BasePath()
    {
#if ANDROID
        return Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDocuments)?.AbsolutePath ?? FileSystem.AppDataDirectory;
#else
        return FileSystem.AppDataDirectory;
#endif
    }
}
