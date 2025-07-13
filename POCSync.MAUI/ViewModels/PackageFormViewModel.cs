using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Poc.Synchronisation.Application.Features.Packages.Commands.CreatePackage;
using Poc.Synchronisation.Application.Features.Packages.Commands.UpdatePackage;
using Poc.Synchronisation.Domain;
using Poc.Synchronisation.Domain.Abstractions;
using Poc.Synchronisation.Domain.Abstractions.Repositories;
using Poc.Synchronisation.Domain.Abstractions.Services;
using Poc.Synchronisation.Domain.Models;
using POCSync.MAUI.Models;
using POCSync.MAUI.Services.Abstractions;
using POCSync.MAUI.Tools;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace POCSync.MAUI.ViewModels;

[QueryProperty(nameof(PackageJson), "PackageJson")]
public partial class PackageFormViewModel(
    IPackageService service,
    IDocumentService documentService,
    IPackageImageService packageImageService,
    IBaseRepository<StoredEvent, Guid> storeEvent,
    IPackagerDocumentRepository packageDocumentRepo,
    FileManager fileManager
    ) : BaseViewModel
{
    [ObservableProperty]
    private string reference;

    [ObservableProperty]
    private decimal? weight;

    [ObservableProperty]
    private decimal? volume;

    [ObservableProperty]
    private decimal? tareWeight;

    [ObservableProperty]
    private string packageJson = string.Empty;

    [ObservableProperty]
    private Guid? packageId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotEditMode))]
    private bool isEditMode;

    [ObservableProperty]
    private ImageSource fullPath;

    public bool IsNotEditMode => !IsEditMode;

    [ObservableProperty]
    private string eventJson = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private ObservableCollection<ImageModel> imageSources = [];
    private IReadOnlyCollection<PackageDocument> docs { get; set; } = [];
    // Handle the deserialization when packageJson is set
    partial void OnPackageJsonChanged(string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            try
            {
                var package = JsonSerializer.Deserialize<Package>(value);
                if (package != null)
                {
                    // Populate the form fields
                    PackageId = package.Id;
                    Reference = package.Reference;
                    Weight = package.Weight;
                    Volume = package.Volume;
                    TareWeight = package.TareWeight;
                    IsEditMode = true;
                    FillEvents(package);
                    var documents = packageDocumentRepo.GetByPackageIdAsync(package.Id).Result;
                    docs = documents;
                    var paths = documents
                        .Select(doc => doc.Document.StorageUrl)
                        .Where(x => x != null)
                        .Select(fileManager.GetImageSourceFromPath)
                        .ToList();

                    foreach (var path in paths)
                    {
                        if (path is not null)
                        {
                            ImageSources.Add(path);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Shell.Current.DisplayAlert("Error", $"Failed to load package data: {ex.Message}", "OK");
            }
        }
    }

    [RelayCommand]
    async Task OnCreatePackage()
    {
        if (string.IsNullOrWhiteSpace(Reference))
        {
            await Shell.Current.DisplayAlert("Validation Error", "Reference is required", "OK");
            return;
        }

        try
        {
            IsBusy = true;

            var command = new CreatePackageCommand(Guid.Empty, Reference, Weight ?? 0, Volume ?? 0, TareWeight ?? 0);
            var result = await service.AddPackageAsync(command, [.. ImageSources.Select(x => x.Path)]);

            if (result)
            {
                await Shell.Current.GoToAsync("..");
            }
            else
            {
                await Shell.Current.DisplayAlert("Error", "Failed to create package", "OK");
            }
        }
        catch (Exception ex)
        {
            // Log the exception if you have logging
            // logger.LogError(ex, "Error creating package");

            await Shell.Current.DisplayAlert("Error", $"An error occurred while creating the package: {ex.Message}", "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    async Task OnUpdatePackage()
    {
        if (string.IsNullOrWhiteSpace(Reference))
        {
            await Shell.Current.DisplayAlert("Validation Error", "Reference is required", "OK");
            return;
        }

        if (!PackageId.HasValue)
        {
            await Shell.Current.DisplayAlert("Error", "Package ID is missing", "OK");
            return;
        }

        try
        {
            IsBusy = true;

            var command = new UpdatePackageCommand(PackageId.Value, Reference, Weight ?? 0, Volume ?? 0, TareWeight ?? 0);
            var result = await service.UpdatePackageAsync(command, [.. ImageSources.Select(x => x.Path)]);

            if (result)
            {
                await Shell.Current.GoToAsync("..");
            }
            else
            {
                await Shell.Current.DisplayAlert("Error", "Failed to update package", "OK");
            }
        }
        catch (Exception ex)
        {
            // Log the exception if you have logging
            // logger.LogError(ex, "Error updating package");

            await Shell.Current.DisplayAlert("Error", $"An error occurred while updating the package: {ex.Message}", "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    async Task OnAddImage()
    {
        // 1) Capture the photo
        if (!MediaPicker.Default.IsCaptureSupported)
            throw new NotSupportedException("Camera capture not supported on this device.");

        var photo = await MediaPicker.Default.CapturePhotoAsync(
            new MediaPickerOptions { Title = "Snap a photo" }
        );

        if (photo == null)
            return;

        using Stream source = await photo.OpenReadAsync();
        var fileName = photo.FileName;
        try
        {
            var path = await documentService.CreateDocumentAsync(source, fileName, ["packages"]);
            var src = fileManager.GetImageSourceFromPath(path);
            if (src is not null)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    ImageSources.Add(src);
                });

            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"Failed to save image: {ex.Message}", "OK");
        }
    }

    [RelayCommand]
    async Task Cancel()
    {

    }

    [RelayCommand]
    async Task OnDeleteImage(string path)
    {
        var itemToRemove = ImageSources.FirstOrDefault(x => x.Path == path);
        if (itemToRemove is null)
            return;
        await documentService.DeleteDocumentAsync(path);
        ImageSources.Remove(itemToRemove);

    }

    void FillEvents(Package package)
    {
        var events = storeEvent.GetAllAsync().Result.Where((x => x.MobileEventId == package.Id));
        var debug = JsonSerializer.Serialize(events);
        EventJson = JsonSerializer.Serialize(events.FirstOrDefault(x => x.MobileEventId == package.Id));
    }
}
