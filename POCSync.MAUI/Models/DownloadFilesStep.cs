namespace POCSync.MAUI.Models;

public class DownloadFilesStep
{
    public string Description { get; set; } = string.Empty;
    public double Progress { get; set; }
    public bool IsDone { get; set; } = false;
    public bool IsError { get; set; } = false;
    public string? ErrorDescription { get; set; }

    // Private constructor to prevent direct instantiation
    private DownloadFilesStep() { }

    // Static factory method for success
    public static DownloadFilesStep CreateSuccess(string description, double progress = 1.0)
    {
        return new DownloadFilesStep
        {
            Description = description,
            Progress = progress,
            IsDone = true,
            IsError = false,
            ErrorDescription = null
        };
    }

    // Static factory method for error
    public static DownloadFilesStep CreateError(string description, string errorDescription, double progress = 0.0)
    {
        return new DownloadFilesStep
        {
            Description = description,
            Progress = progress,
            IsDone = false,
            IsError = true,
            ErrorDescription = errorDescription
        };
    }
}
