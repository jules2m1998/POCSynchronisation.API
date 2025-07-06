using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Dapper.Abstractions;
using Infrastructure.Dapper.Repository;
using Infrastructure.Dapper.Services.Generated;
using Mapster;
using Mediator.Abstractions;
using Microsoft.Extensions.Logging;
using Poc.Synchronisation.Application;
using Poc.Synchronisation.Application.Features.Synchronisation.Commands.Synchronisation;
using Poc.Synchronisation.Domain;
using Poc.Synchronisation.Domain.Abstractions;
using POCSync.MAUI.Extensions;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace POCSync.MAUI.ViewModels;

public partial class SynchronisationViewModel(
        IApi api,
        IInitialiser initialiser,
        IBaseRepository<User, Guid> userRepo,
        IBaseRepository<StoredEvent, Guid> storeEventRepo,
        ISender sender,
        IDbConnectionFactory db,
        ILogger<SynchronisationViewModel> logger,
        IEnumerable<IEventIdUpdater> IdUpdaters
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
        logger.LogInformation("Starting initialization process");
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var users = await userRepo.GetAllAsync();
            logger.LogInformation("Retrieved {UserCount} users from database", users.Count());

            user = users.FirstOrDefault();
            if (user is null)
            {
                logger.LogWarning("No user found in database, initialization required");
                IsInitialisation = true;
                return;
            }

            logger.LogInformation("User found: {UserId}, LastEventSynced: {LastEventId}, IsInitialised: {IsInitialised}",
                user.Id, user.LastEventSynced, user.IsInitialised);

            if (user.LastEventSynced != Guid.Empty && user.IsInitialised)
            {
                logger.LogInformation("User already initialized, switching to sync mode");
                IsInitialisation = false;
                return;
            }

            logger.LogInformation("User requires initialization");
            IsInitialisation = true;

            var allEvents = await storeEventRepo.GetAllAsync();
            logger.LogInformation("Retrieved {EventCount} stored events for JSON serialization", allEvents.Count());

            StoredEventJson = JsonSerializer.Serialize(allEvents);
            logger.LogDebug("Stored events JSON size: {StoredEventJson} characters", StoredEventJson);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during initialization process");
            throw;
        }
        finally
        {
            stopwatch.Stop();
            logger.LogInformation("Initialization completed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
        }
    }

    [RelayCommand]
    async Task Retrieve()
    {
        logger.LogInformation("Starting data retrieval process");
        var stopwatch = Stopwatch.StartNew();
        IsBusy = true;

        try
        {
            // Call the API to synchronise data
            CurrentProgress = 0.5;
            ProgressTitle = "Retrieving data.";
            logger.LogInformation("Calling API to retrieve data");

            var data = await api.Retrieve();
            logger.LogInformation("Successfully retrieved {DataCount} items from API", data?.Count ?? 0);

            CurrentProgress = 0.6;
            ProgressTitle = "Saving data";

            var storedData = data.Adapt<ICollection<StoredRetiever>>();
            logger.LogInformation("Mapped {StoredDataCount} items for initialization", storedData.Count);

            var result = await initialiser.Initialise(storedData);
            logger.LogInformation("Initialization completed with result: {Result}", result);

            CurrentProgress = 1;
            ProgressTitle = "Saving ended";

            if (user is not null)
            {
                logger.LogInformation("Updating user {UserId} with last saved event", user.Id);
                var response = await api.LastSavedEventId(user.Id);

                if (response is not null)
                {
                    logger.LogInformation("Retrieved last saved event ID: {EventId}", response.Id);
                    user.LastEventSynced = response.Id ?? Guid.Empty;
                    user.IsInitialised = true;

                    await userRepo.UpdateAsync(user);
                    logger.LogInformation("User {UserId} updated successfully", user.Id);

                    await Initialisation();
                }
                else
                {
                    logger.LogWarning("API returned null response for LastSavedEventId");
                }
            }
            else
            {
                logger.LogWarning("User is null, cannot update last saved event");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during data retrieval process");
            await Shell.Current.DisplayAlert("Error", $"An error occurred during data retrieval: {ex.Message}", "OK");
        }
        finally
        {
            IsBusy = false;
            stopwatch.Stop();
            logger.LogInformation("Data retrieval completed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
        }
    }

    [RelayCommand]
    async Task Synchronise()
    {
        logger.LogInformation("Starting synchronization process for user {UserId}", user?.Id);
        var stopwatch = Stopwatch.StartNew();
        IsBusy = true;

        try
        {
            await SendEventsAsync();
            var allEvents = await storeEventRepo.GetAllAsync();

            ProgressTitle = "Fetching events";
            logger.LogInformation("Fetching non-synced events from server. LastEventSynced: {LastEventId}",
                user?.LastEventSynced ?? Guid.Empty);

            var result = await api.NonSyncedEvents(user?.LastEventSynced ?? Guid.Empty, user.Id);
            logger.LogInformation("Retrieved {EventCount} non-synced events from server", result?.Count ?? 0);

            var eventsToSync = result.Adapt<ICollection<StoredEvent>>();
            logger.LogInformation("Mapped {EventCount} events for synchronization", eventsToSync.Count);

            await ApplyEventAsync(eventsToSync);
            logger.LogInformation("Synchronization process completed successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during synchronization process");
            await Shell.Current.DisplayAlert("Error", $"An error occurred while synchronizing: {ex.Message}", "OK");
        }
        finally
        {
            IsBusy = false;

            try
            {
                var allEvents = await storeEventRepo.GetAllAsync();
                StoredEventJson = JsonSerializer.Serialize(allEvents);
                logger.LogInformation("Updated StoredEventJson with {EventCount} events", allEvents.Count());
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error updating StoredEventJson");
            }

            stopwatch.Stop();
            logger.LogInformation("Synchronization completed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
        }
    }

    [RelayCommand]
    async Task ResetTables()
    {
        logger.LogInformation("Starting table reset operation");
        var stopwatch = Stopwatch.StartNew();

        try
        {
            db.CleanDb();
            logger.LogInformation("Database tables reset successfully");

            IsInitialisation = true;
            ProgressTitle = "Resetting tables";
            ProgressMessage = "Tables reset successfully.";

            logger.LogInformation("UI updated to initialization mode");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during table reset operation");
            throw;
        }
        finally
        {
            stopwatch.Stop();
            logger.LogInformation("Table reset completed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
        }
    }

    private async Task ApplyEventAsync(ICollection<StoredEvent> eventsToSync)
    {
        logger.LogInformation("Starting to apply {EventCount} events", eventsToSync.Count);
        var stopwatch = Stopwatch.StartNew();
        var lastEventId = user?.LastEventSynced;
        var processedEvents = 0;

        try
        {
            var total = eventsToSync.Count;
            var iteration = 0;

            foreach (var batch in eventsToSync.Batch(10))
            {
                var batchList = batch.ToList();
                logger.LogDebug("Processing batch {BatchNumber} with {BatchSize} events", iteration + 1, batchList.Count);

                var command = new SynchronisationCommand(batchList, Guid.Empty);
                var data = await sender.Send(command);

                logger.LogDebug("Batch {BatchNumber} processed successfully, returned {ResultCount} events",
                    iteration + 1, data.Events?.Count ?? 0);

                var progress = (double)iteration / total;
                CurrentProgress = progress * 0.6 + 0.4;

                var last = data?.Events?.LastOrDefault();
                if (last is not null)
                {
                    lastEventId = last.EventId;
                    logger.LogDebug("Updated lastEventId to {EventId}", lastEventId);
                }

                processedEvents += batchList.Count;
                iteration++;
            }

            ProgressTitle = "Saving ended";
            logger.LogInformation("Successfully applied {ProcessedEvents}/{TotalEvents} events", processedEvents, eventsToSync.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error applying events. Processed {ProcessedEvents}/{TotalEvents}",
                processedEvents, eventsToSync.Count);
        }
        finally
        {
            if (user is not null && lastEventId is not null)
            {
                user.LastEventSynced = lastEventId ?? Guid.Empty;
                user.IsInitialised = true;

                await userRepo.UpdateAsync(user);
                logger.LogInformation("Updated user {UserId} with LastEventSynced: {LastEventId}",
                    user.Id, lastEventId);
            }
            else
            {
                logger.LogWarning("Cannot update user: User={UserNull}, LastEventId={EventIdNull}",
                    user is null, lastEventId is null);
            }

            stopwatch.Stop();
            logger.LogInformation("ApplyEventAsync completed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
        }
    }

    private async Task SendEventsAsync()
    {
        logger.LogInformation("Starting to send events to server");
        var stopwatch = Stopwatch.StartNew();
        var resultEventa = new List<StoredEvent>();
        var processedBatches = 0;
        var deletedEvents = 0;

        try
        {
            CurrentProgress = 0.0;
            ProgressTitle = "Synchronising data.";

            var events = await storeEventRepo.GetAllAsync();
            var eventId = events.FirstOrDefault()?.EventId;
            var MobileEventId = events.FirstOrDefault()?.MobileEventId;
            var eventsArray = events.ToArray();
            logger.LogInformation("Retrieved {EventCount} events to send to server", eventsArray.Length);

            var total = eventsArray.Length;
            var iteration = 0;

            foreach (var batch in eventsArray.Batch(10))
            {
                var batchList = batch.ToList();
                logger.LogDebug("Sending batch {BatchNumber}/{TotalBatches} with {BatchSize} events",
                    iteration + 1, (total + 9) / 10, batchList.Count);

                var dto = batchList.Adapt<List<SynchronisedStoredEventDto>>();
                var request = new SynchronisationRequest
                {
                    Events = [.. dto.Select(x =>
                    {
                        x.LastSyncEvent = user?.LastEventSynced ?? Guid.Empty;
                        return x;
                    })]
                };

                logger.LogDebug("Sending synchronization request with {request}", JsonSerializer.Serialize(request));
                var data = await SynchronizeWithServerAsync(request);
                logger.LogDebug("Server responded with {data}", JsonSerializer.Serialize(data));

                var createEvents = data?
                    .Results?
                    .Where(x => x.EventType.StartsWith("create", StringComparison.CurrentCultureIgnoreCase))
                    .ToList();

                logger.LogDebug("Found {createEvents} create events in response", JsonSerializer.Serialize(createEvents));
                resultEventa.AddRange(createEvents.Adapt<List<StoredEvent>>());

                iteration++;
                var progress = (double)iteration / total;
                processedBatches++;

                var batchDeletedCount = 0;

                foreach (var result in data.Results)
                {
                    if (result.EventStatus == EventType.Success || result.EventStatus == EventType.Conflict)
                    {
                        var id = result.EventId;
                        var deleteResult = await storeEventRepo.DeleteByIdAsync(id);
                        if (deleteResult)
                        {
                            batchDeletedCount++;
                            deletedEvents++;
                        }
                        logger.LogDebug("Deleted event {EventId}, success: {DeleteSuccess}", id, deleteResult);
                    }
                }

                logger.LogDebug("Batch {BatchNumber} completed: deleted {DeletedCount} events",
                    iteration, batchDeletedCount);
                CurrentProgress = progress * 0.6;
            }

            logger.LogInformation("Successfully sent {ProcessedBatches} batches, deleted {DeletedEvents} events, created {CreatedEvents} new events",
                processedBatches, deletedEvents, resultEventa.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending events to server. Processed {ProcessedBatches} batches, deleted {DeletedEvents} events",
                processedBatches, deletedEvents);
            await Shell.Current.DisplayAlert("Error", $"An error occurred while sending events: {ex.Message}", "OK");
        }
        finally
        {
            try
            {
                logger.LogInformation("Found {UpdaterCount} event ID updaters, updating {ResultEventCount} events",
                    IdUpdaters.Count(), resultEventa.Count);

                foreach (var service in IdUpdaters)
                {
                    var result = await service.UpdateEventId(resultEventa);
                    logger.LogDebug("Event ID updater {UpdaterType} completed with result: {Result}",
                        service.GetType().Name, result);
                }

                logger.LogInformation("Event ID update process completed");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error updating event IDs");
            }

            stopwatch.Stop();
            logger.LogInformation("SendEventsAsync completed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
        }
    }

    private async Task<SynchronisationResponse> SynchronizeWithServerAsync(SynchronisationRequest request)
    {
        logger.LogInformation("Starting server synchronization with {EventCount} events", request.Events?.Count ?? 0);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Create a handler that accepts all certificates (WARNING: ONLY FOR DEVELOPMENT)
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };

            // For Android specifically, use AndroidMessageHandler
            if (DeviceInfo.Platform == DevicePlatform.Android)
            {
                logger.LogDebug("Using Android-specific HTTP handler");
                handler = new()
                {
                    ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
                };
            }

            using var httpClient = new HttpClient(handler);
            var baseUrl = "https://localhost:7199/";
            httpClient.BaseAddress = new Uri(baseUrl);
            logger.LogDebug("HTTP client configured with base URL: {BaseUrl}", baseUrl);

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
            logger.LogDebug("Serialized request size: {RequestSize} characters", jsonContent.Length);

            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            logger.LogInformation("Sending POST request to api/Synchronisation");
            var response = await httpClient.PostAsync("api/Synchronisation", content);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                logger.LogInformation("Received successful response with {ResponseSize} characters", responseContent.Length);

                var result = JsonSerializer.Deserialize<SynchronisationResponse>(responseContent, jsonOptions);
                logger.LogInformation("Deserialized response with {ResultCount} results", result?.Results?.Count ?? 0);

                return result;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                logger.LogError("Synchronization failed with status {StatusCode}: {ErrorContent}",
                    response.StatusCode, errorContent);

                throw new HttpRequestException(
                    $"Synchronization failed with status code {response.StatusCode}: {errorContent}",
                    null,
                    response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during server synchronization");
            await Shell.Current.DisplayAlert("Error", $"An error occurred while synchronizing with server: {ex.Message}", "OK");
            throw;
        }
        finally
        {
            stopwatch.Stop();
            logger.LogInformation("SynchronizeWithServerAsync completed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
        }
    }
}