using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Poc.Synchronisation.Application.Features.Packages.Commands.CreatePackage;
using Poc.Synchronisation.Application.Features.Packages.Commands.UpdatePackage;
using Poc.Synchronisation.Domain;
using Poc.Synchronisation.Domain.Abstractions;
using Poc.Synchronisation.Domain.Models;
using POCSync.MAUI.Services.Abstractions;
using System.Text.Json;

namespace POCSync.MAUI.ViewModels;

[QueryProperty(nameof(PackageJson), "PackageJson")]
public partial class PackageFormViewModel(IPackageService service, IBaseRepository<StoredEvent, Guid> storeEvent) : BaseViewModel
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

    public bool IsNotEditMode => !IsEditMode;

    [ObservableProperty]
    private string eventJson = string.Empty;

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

            var command = new CreatePackageCommand(Reference, Weight ?? 0, Volume ?? 0, TareWeight ?? 0);
            var result = await service.AddPackageAsync(command);

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
            var result = await service.UpdatePackageAsync(command);

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

    void FillEvents(Package package)
    {
        var events = storeEvent.GetAllAsync().Result.Where((x => x.MobileEventId == package.Id));
        var debug = JsonSerializer.Serialize(events);
        EventJson = JsonSerializer.Serialize(events.FirstOrDefault(x => x.MobileEventId == package.Id));
    }
}
