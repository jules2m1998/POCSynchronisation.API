using Infrastructure.Dapper.Abstractions;

namespace POCSync.MAUI.Tools;

public class PermisionManager : IPermissionManger
{
    public async Task<bool> CheckAndRequestStoragePermission()
    {
        var status = await Permissions.CheckStatusAsync<Permissions.StorageRead>();
        var writeStatus = await Permissions.CheckStatusAsync<Permissions.StorageWrite>();

        if (status != PermissionStatus.Granted)
        {
            status = await Permissions.RequestAsync<Permissions.StorageRead>();
        }

        if (writeStatus != PermissionStatus.Granted)
        {
            writeStatus = await Permissions.RequestAsync<Permissions.StorageWrite>();
        }

        return status == PermissionStatus.Granted && writeStatus == PermissionStatus.Granted;
    }

    public static string GetStoragePath()
    {
#if ANDROID
        return Android.OS
            .Environment
            .GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDocuments)?
            .AbsolutePath ?? FileSystem.AppDataDirectory;
#else
        return FileSystem.AppDataDirectory;
#endif
    }
}
