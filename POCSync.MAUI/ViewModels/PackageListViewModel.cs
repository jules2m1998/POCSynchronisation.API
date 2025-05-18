using CommunityToolkit.Mvvm.Input;
using Infrastructure.Dapper.Services.Generated;
using Poc.Synchronisation.Domain.Models;
using POCSync.MAUI.Services.Abstractions;
using POCSync.MAUI.Views;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace POCSync.MAUI.ViewModels;

public partial class PackageListViewModel(IPackageService service) : BaseViewModel
{
    public ObservableCollection<Package> Packages { get; set; } = [];

    [RelayCommand]
    async Task LoadPackages()
    {
        IsBusy = true;
        try
        {
            var result = await service.GetAllPackagesAsync();
            Packages.Clear();
            foreach (var package in result.OrderByDescending(x => x.CreatedAt))
            {
                Packages.Add(package);
            }
        }
        catch (Exception ex)
        {
            // Handle exceptions
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    async Task GotToGreate()
    {
        await Shell.Current.GoToAsync(nameof(PackageFormPage));
    }

    [RelayCommand]
    async Task EditPackage(Package package)
    {
        var navigationParameter = new Dictionary<string, object>
        {
            { "PackageJson", JsonSerializer.Serialize(package) }
        };

        await Shell.Current.GoToAsync(nameof(PackageFormPage), navigationParameter);
    }

    [RelayCommand]
    async Task DeletePackage(Package package)
    {
        bool confirm = await Shell.Current.DisplayAlert(
                "Confirm Delete",
                $"Are you sure you want to delete the package '{package.Reference}'?",
                "Yes", "No");
        if (!confirm)
        {
            return;
        }

        try
        {
            IsBusy = true;
            var success = await service.DeletePackageAsync(package.Id);
            if (success)
            {
                var itemToRemove = Packages.FirstOrDefault(p => p.Id == package.Id);
                if (itemToRemove != null)
                {
                    Packages.Remove(itemToRemove);
                }

                await Shell.Current.DisplayAlert("Success", "Package deleted successfully", "OK");
            }
            else
            {
                await Shell.Current.DisplayAlert("Error", "Failed to delete package", "OK");
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"Error deleting package: {ex.Message}", "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
