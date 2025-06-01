using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Dapper.Abstractions;
using Infrastructure.Dapper.Repository;
using Infrastructure.Dapper.Services.Generated;
using Mapster;
using Mediator.Abstractions;
using Poc.Synchronisation.Application;
using Poc.Synchronisation.Application.Features.Synchronisation.Commands.Synchronisation;
using Poc.Synchronisation.Domain;
using Poc.Synchronisation.Domain.Abstractions;
using POCSync.MAUI.Extensions;
using System.Text;
using System.Text.Json;

namespace POCSync.MAUI.ViewModels;

public partial class SynchronisationViewModel(
        IApi api,
        IInitialiser initialiser,
        IBaseRepository<User, Guid> userRepo,
        IBaseRepository<StoredEvent, Guid> storeEventRepo,
        IServiceProvider serviceProvider,
        ISender sender,
        IDbConnectionFactory db
    ) : BaseViewModel
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotInitialisation))]
    bool isInitialisation = true;

    [ObservableProperty]
    string progressTitle;

    [ObservableProperty]
    string progressMessage;

    [ObservableProperty]
    double currentProgress;

    [ObservableProperty]
    string storedEventJson = "";


    public bool IsNotInitialisation => !IsInitialisation;

    private User? user { get; set; }

    [RelayCommand]
    async Task Initialisation()
    {
        var users = await userRepo.GetAllAsync();
        user = users.First();
        if (user is null)
        {
            IsInitialisation = true;
            return;
        }
        if (user.LastEventSynced != Guid.Empty && user.IsInitialised)
        {
            IsInitialisation = false;
            return;
        }
        IsInitialisation = true;
        StoredEventJson = JsonSerializer.Serialize(await storeEventRepo.GetAllAsync());
    }


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
            if (user is not null)
            {
                var response = await api.LastSavedEventId(user.Id);
                if (response is not null)
                {
                    user.LastEventSynced = response.Id ?? Guid.Empty;
                    user.IsInitialised = true;
                    await userRepo.UpdateAsync(user);
                    await Initialisation();
                }
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
    async Task Synchronise()
    {
        IsBusy = true;
        try
        {
            await SendEventsAsync();

            ProgressTitle = "Fetching events";
            var result = await api.NonSyncedEvents(user?.LastEventSynced ?? Guid.Empty, user.Id);
            var eventsToSync = result.Adapt<ICollection<StoredEvent>>();
            await ApplyEventAsync(eventsToSync);
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"An error occurred while synchronizing the package: {ex.Message}", "OK");
        }
        finally
        {
            IsBusy = false;
            StoredEventJson = JsonSerializer.Serialize(await storeEventRepo.GetAllAsync());
        }
    }

    [RelayCommand]
    async Task ResetTables()
    {
        db.CleanDb();
        IsInitialisation = true;
        ProgressTitle = "Resetting tables";
        ProgressMessage = "Tables reset successfully.";
    }

    private async Task ApplyEventAsync(ICollection<StoredEvent> eventsToSync)
    {
        var lastEventId = user?.LastEventSynced;
        try
        {
            var total = eventsToSync.Count;
            var iteration = 0;
            foreach (var item in eventsToSync.Batch(10))
            {
                var command = new SynchronisationCommand(item, Guid.Empty);
                var data = await sender.Send(command);
                var progress = (double)iteration / total;
                CurrentProgress = progress * 0.6 + 0.4;
                var last = data.Events.LastOrDefault();
                if (last is not null)
                {
                    lastEventId = last.EventId;
                }
            }
            ProgressTitle = "Saving ended";

        }
        catch
        {

        }
        finally
        {
            if (user is not null && lastEventId is not null)
            {
                user.LastEventSynced = lastEventId ?? Guid.Empty;
                user.IsInitialised = true;
                await userRepo.UpdateAsync(user);
            }
        }

    }

    private async Task SendEventsAsync()
    {
        var resultEventa = new List<StoredEvent>();
        try
        {
            // Call the API to synchronise data
            CurrentProgress = 0.0;
            ProgressTitle = "Synchronising data.";
            var @events = await storeEventRepo.GetAllAsync();
            var total = @events.Count();
            var iteration = 0;

            foreach (var item in @events.Batch(10))
            {
                var dto = item.Adapt<List<SynchronisedStoredEventDto>>();
                var request = new SynchronisationRequest
                {
                    Events = [.. dto.Select(x =>
                    {
                        x.LastSyncEvent = user?.LastEventSynced ?? Guid.Empty;
                        return x;
                    })]
                };

                var data = await SynchronizeWithServerAsync(request);
                var createEvents = data
                    .Results
                    .Where(x => x.EventType.StartsWith("create", StringComparison.CurrentCultureIgnoreCase))
                    .ToList();

                resultEventa.AddRange(createEvents.Adapt<List<StoredEvent>>());
                iteration++;
                var progress = (double)iteration / total;

                foreach (var result in data.Results)
                {
                    if (result.EventStatus == EventType.Success)
                    {
                        var id = result.MobileEventId;
                        var r = await storeEventRepo.DeleteByIdAsync(id);
                    }
                }

                CurrentProgress = progress * 0.6;

            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"An error occurred while Sending Events the package: {ex.Message}", "OK");
        }
        finally
        {

            var idUpdater = serviceProvider.GetServices<IEventIdUpdater>();
            foreach (var service in idUpdater)
            {
                var result = await service.UpdateEventId(resultEventa);
            }
        }
    }

    private async Task<SynchronisationResponse> SynchronizeWithServerAsync(SynchronisationRequest request)
    {
        // Create a handler that accepts all certificates (WARNING: ONLY FOR DEVELOPMENT)
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        };

        // For Android specifically, use AndroidMessageHandler
        if (DeviceInfo.Platform == DevicePlatform.Android)
        {
            handler = new()
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };
        }

        using var httpClient = new HttpClient(handler);
        //httpClient.BaseAddress = new Uri("https://10.0.2.2:7199/");
        httpClient.BaseAddress = new Uri("https://localhost:7199/");

        // Configure headers
        httpClient.DefaultRequestHeaders.Accept.Add(
            new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

        // Serialize the request
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        var jsonContent = JsonSerializer.Serialize(request, jsonOptions);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        try
        {
            var response = await httpClient.PostAsync("api/Synchronisation", content);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<SynchronisationResponse>(responseContent, jsonOptions);
                return result;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException(
                    $"Synchronization failed with status code {response.StatusCode}: {errorContent}",
                    null,
                    response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"An error occurred while synchronize with serverAsync the package: {ex.Message}", "OK");
            throw;
        }
    }
}
