using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Dapper.Services.Generated;
using Mapster;
using Poc.Synchronisation.Application;

namespace POCSync.MAUI.ViewModels;

public partial class SynchronisationViewModel(IApi api, IInitialiser initialiser) : BaseViewModel
{
    [ObservableProperty]
    bool isInitialisation = true;

    [ObservableProperty]
    string progressTitle;

    [ObservableProperty]
    string progressMessage;

    [ObservableProperty]
    double currentProgress;


    [RelayCommand]
    async Task Retrieve()
    {
        IsBusy = true;
        try
        {
            // Call the API to synchronise data
            CurrentProgress = 0.5;
            ProgressTitle = "Retrieving data.";
            var data = await api.Retrieve();
            CurrentProgress = 0.6;
            ProgressTitle = "Saving data";
            var storedData = data.Adapt<ICollection<StoredRetiever>>();
            var result = await initialiser.Initialise(storedData);
            CurrentProgress = 1;
            ProgressTitle = "Saving ended";
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
}
