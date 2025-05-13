namespace POCSync.MAUI.Helpers;
public class PermissionHelper
{
    public static async Task<bool> CheckAndRequestStoragePermission()
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

        // For Android 11+ (API 30+), you need to use the system file picker
        // or implement special handling with MANAGE_EXTERNAL_STORAGE

#if ANDROID
        // For Android 10+ (API 29+), additional handling for full storage access
        if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.R)
        {
            // Check if we have all files access permission
            if (!Android.OS.Environment.IsExternalStorageManager)
            {
                // Request all files access permission
                var intent = new Android.Content.Intent(
                    Android.Provider.Settings.ActionManageAppAllFilesAccessPermission,
                    Android.Net.Uri.Parse($"package:{Android.App.Application.Context.PackageName}"));

                Platform.CurrentActivity.StartActivity(intent);
                return false; // User needs to grant permission in settings
            }
        }
#endif

        return status == PermissionStatus.Granted && writeStatus == PermissionStatus.Granted;
    }
}