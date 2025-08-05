namespace POCSync.MAUI.Models;

public class PackageModel
{
    public Guid Id { get; set; }
    public string Reference { get; set; } = string.Empty;
    public decimal? Weight { get; set; }
    public decimal? Volume { get; set; }
    public decimal? TareWeight { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsConflicted { get; set; } = false;
    public bool IsNotConflicted => !IsConflicted;

    public ICollection<ImageModel> Images { get; set; } = [];

    public ImageSource? FirstImage => Images.FirstOrDefault()?.Source;
}
