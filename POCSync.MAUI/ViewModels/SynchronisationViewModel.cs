using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Infrastructure.Dapper;
using Infrastructure.Dapper.Abstractions;
using Infrastructure.Dapper.Repository;
using Infrastructure.Dapper.Services.Abstractions;
using Infrastructure.Dapper.Services.Generated;
using Mapster;
using Mediator.Abstractions;
using Microsoft.Extensions.Logging;
using Poc.Synchronisation.Application;
using Poc.Synchronisation.Application.Features.Synchronisation.Commands.Synchronisation;
using Poc.Synchronisation.Domain;
using Poc.Synchronisation.Domain.Abstractions;
using Poc.Synchronisation.Domain.Abstractions.Services;
using POCSync.MAUI.Extensions;
using POCSync.MAUI.Models;
using POCSync.MAUI.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
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
        IEnumerable<IEventIdUpdater> IdUpdaters,
        IFileTransferService fileTransferService,
        IDBForeignKeyMode keyMod,
        DatabaseInitializer _databaseInitializer,
        SynchronisationService synchronisationService
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

    [ObservableProperty]
    int countDataToSync = 0;

    [ObservableProperty]
    int countDocToSync = 0;

    [ObservableProperty]
    private ObservableCollection<SynchroStep> synchroSteps = [];

    [ObservableProperty]
    private SynchroReport report = new();

    [ObservableProperty]
    bool isThereSomethingToSync = true;

    public bool IsNotInitialisation => !IsInitialisation;

    private User? user { get; set; }

    [RelayCommand]
    async Task Initialisation()
    {
        logger.LogInformation("Starting initialization process");
        var stopwatch = Stopwatch.StartNew();
        var info = await synchronisationService.GetSynchronisationInfoAsync();
        if (info.TotalEvents > 0 || info.TotalDocumentsToSync > 0)
        {
            CountDataToSync = info.TotalEvents;
            CountDocToSync = info.TotalDocumentsToSync;
        }
        else
        {
            IsThereSomethingToSync = false;
        }

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
            IsThereSomethingToSync = true;

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
        db.CleanDb();
        await _databaseInitializer.InitializeDatabaseAsync();
        await Initialisation();

        SynchroSteps.Clear();
        IsBusy = true;
        var stopwatch = Stopwatch.StartNew();
        logger.LogInformation("Starting data retrieval process");

        try
        {
            // Step 1 - Retrieve data from API
            var step1 = CreateStep("Récupération des données du serveur", "Données en cours de récupération", 0.1);
            SynchroSteps.Add(step1);

            ProgressTitle = "Retrieving data...";
            CurrentProgress = 0.2;

            var data = await api.Retrieve();
            var itemCount = data?.Count ?? 0;

            UpdateStep(step1, $"{itemCount} élément(s) récupéré(s) avec succès !", 0.5);

            logger.LogInformation("Retrieved {DataCount} items from API", itemCount);

            // Step 2 - Save data to DB
            ProgressTitle = "Saving data...";
            CurrentProgress = 0.6;

            var storedData = data?.Adapt<ICollection<StoredRetiever>>() ?? [];
            logger.LogInformation("Mapped {StoredDataCount} items for initialization", storedData.Count);

            UpdateStep(step1, "Sauvegarde des données en base de données...", 0.6);

            await keyMod.Off();
            var result = await initialiser.Initialise(storedData);
            await keyMod.On();

            logger.LogInformation("Data initialization result: {Result}", result);

            UpdateStep(step1, "Données sauvegardées avec succès", 1.0, isCompleted: true);

            CurrentProgress = 0.8;
            ProgressTitle = "Saving complete";
            await DownloadFolders();

            // Step 4 - Update user
            if (user is not null)
            {
                logger.LogInformation("Updating user {UserId}...", user.Id);
                var response = await api.LastSavedEventId(user.Id);

                if (response is not null)
                {
                    user.LastEventSynced = response.Id ?? Guid.Empty;
                    user.IsInitialised = true;

                    await userRepo.UpdateAsync(user);
                    logger.LogInformation("User {UserId} updated successfully", user.Id);

                    await Initialisation();
                }
                else
                {
                    logger.LogWarning("LastSavedEventId returned null for user {UserId}", user.Id);
                }
            }
            else
            {
                logger.LogWarning("User is null, cannot update.");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during data retrieval process");
            await Shell.Current.DisplayAlert("Erreur", $"Une erreur s'est produite : {ex.Message}", "OK");
        }
        finally
        {
            IsBusy = false;
            stopwatch.Stop();
            logger.LogInformation("Data retrieval finished in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
        }
    }

    private async Task DownloadFolders()
    {
        // Step 3 - Download files
        var step2 = CreateStep("Téléchargement des fichiers du serveur", "Initialisation du téléchargement...", 0.1);
        SynchroSteps.Add(step2);

        await foreach (var (description, progress) in fileTransferService.DownloadFiles())
        {
            UpdateStep(step2, description, progress);
            logger.LogInformation("File download progress: {Description} - {Progress:P0}", description, progress);
        }

        UpdateStep(step2, "📁 Fichiers téléchargés avec succès !", 1.0, isCompleted: true);
    }

    [RelayCommand]
    async Task Synchronise()
    {
        SynchroSteps.Clear();
        logger.LogInformation("Starting synchronization process for user {UserId}", user?.Id);
        var stopwatch = Stopwatch.StartNew();
        IsBusy = true;

        try
        {
            await SendEventsAsync();
            ProgressTitle = "Fetching events";
            logger.LogInformation("Fetching non-synced events from server. LastEventSynced: {LastEventId}", user?.LastEventSynced ?? Guid.Empty);

            if (user is null)
            {
                logger.LogWarning("User is null, cannot fetch non-synced events");
                return;
            }

            var result = await api.NonSyncedEvents(user?.LastEventSynced ?? Guid.Empty, user!.Id);
            logger.LogInformation("Retrieved {EventCount} non-synced events from server", result?.Count ?? 0);
            Report.TotalEventToApply = result?.Count ?? 0;
            Report.Isvisible = true;

            var eventsToSync = result.Adapt<ICollection<StoredEvent>>();
            logger.LogInformation("Mapped {EventCount} events for synchronization", eventsToSync.Count);

            await ApplyEventAsync(eventsToSync);
            logger.LogInformation("Synchronization process completed successfully");

            await PushFilesAsync();
            await DownloadFolders();
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
            await Initialisation();
        }
    }

    private async Task PushFilesAsync()
    {
        var currentStep = new SynchroStep
        {
            Step = "",
            Description = "",
            Progress = 0.0,
            IsCompleted = false
        };
        await foreach (var (description, progress, isNewStep, stepTitle, total) in fileTransferService.UploadFiles())
        {
            if (total.HasValue)
            {
                Report.TotalDocumentToSync = total.Value;
            }
            Report.TotalDocumentSynced += 1;

            if (isNewStep && stepTitle != null)
            {
                currentStep.IsCompleted = true;
                currentStep = CreateStep(stepTitle, description, progress);
                SynchroSteps.Add(currentStep);
                continue;
            }
            currentStep.Description = description;
            currentStep.Progress = progress;
        }
        currentStep.IsCompleted = true;
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

    [RelayCommand]
    async Task OnRunProgress()
    {
        SynchroSteps.Clear();
        var step1 = new SynchroStep
        {
            Step = "Test",
            Description = "Test",
            IsCompleted = false,
            Progress = 0.1
        };

        SynchroSteps.Add(step1);


        await Task.Delay(2000);
        step1.Step = "Test 2";
        step1.Description = "Hello";
        step1.Progress = 0.5;

        await Task.Delay(2000);
        step1.Step = "Test 3";
        step1.Description = "Hello world";
        step1.Progress = 0.8;


        var step2 = new SynchroStep
        {
            Step = "Test 3333",
            Description = "Test",
            IsCompleted = false,
            Progress = 0.9
        };


        SynchroSteps.Add(step2);

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
                Report.TotalEventApplied += batchList.Count;
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
        logger.LogInformation("🔄 Starting to send events to server");
        var stopwatch = Stopwatch.StartNew();

        var resultEvents = new List<StoredEvent>();
        var deletedEvents = 0;
        var processedBatches = 0;

        try
        {
            CurrentProgress = 0.0;
            ProgressTitle = "Synchronisation des données...";

            // Step 1: Fetch Events
            var getEventsStep = CreateStep("Récupération des actions à synchroniser", "🔄 Initialisation...", 0);
            SynchroSteps.Add(getEventsStep);

            var events = (await storeEventRepo.GetAllAsync()).ToArray();
            Report.TotalEventToSync = events.Length;
            Report.TotalEventSynced = 0;

            UpdateStep(getEventsStep, $"📁 {events.Length} action(s) récupérée(s) avec succès", 1.0, true);

            if (events.Length == 0)
            {
                logger.LogInformation("Aucune action à synchroniser");
                return;
            }

            var sendStep = CreateStep("Envoi des données au serveur", "Préparation...", 0.0);
            SynchroSteps.Add(sendStep);

            // Step 2: Send in batches
            var batchSize = 10;
            var totalBatches = (int)Math.Ceiling(events.Length / (double)batchSize);
            var iteration = 0;

            foreach (var batch in events.Batch(batchSize))
            {
                iteration++;
                var dtoBatch = batch.Select(e =>
                {
                    var dto = e.Adapt<SynchronisedStoredEventDto>();
                    dto.LastSyncEvent = user?.LastEventSynced ?? Guid.Empty;
                    return dto;
                }).ToList();

                var request = new SynchronisationRequest { Events = dtoBatch };

                logger.LogDebug("Sending batch {Batch}/{Total}", iteration, totalBatches);
                var response = await api.Synchronisation(request);
                logger.LogDebug("Received server response");

                var createdEvents = response?.Results?
                    .Where(x => x.EventType.StartsWith("create", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (createdEvents?.Count > 0)
                    resultEvents.AddRange(createdEvents.Adapt<List<StoredEvent>>());

                // Delete successful or conflict events
                foreach (var result in response.Results)
                {
                    if (result.EventStatus is SynchronisedStoredEventDtoEventStatus.Success or SynchronisedStoredEventDtoEventStatus.Conflict)
                    {
                        if (await storeEventRepo.DeleteByIdAsync(result.EventId))
                            deletedEvents++;
                    }
                }

                processedBatches++;
                var progress = (double)iteration / totalBatches;
                CurrentProgress = progress * 0.6; // up to 60%
                UpdateStep(sendStep, $"Envoi: {Math.Round(progress * 100)}%", progress);
                Report.TotalEventSynced += batch.Count;
                Report.TotalConflict += response.Results?.Count(x => x.EventStatus == SynchronisedStoredEventDtoEventStatus.Conflict) ?? 0;
            }

            UpdateStep(sendStep, "📁 Données envoyées avec succès ✅", 1.0, true);
            logger.LogInformation("📤 Sent {ProcessedBatches} batches, deleted {DeletedEvents} events", processedBatches, deletedEvents);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur pendant la synchronisation des événements");
            await Shell.Current.DisplayAlert("Erreur", $"Une erreur est survenue : {ex.Message}", "OK");
            return;
        }

        // Step 3: Reconciliation
        try
        {
            var reconciliationStep = CreateStep("Réconciliation des données serveur", "En cours...", 0.0);
            SynchroSteps.Add(reconciliationStep);

            var index = 0;
            var count = IdUpdaters.Count();

            foreach (var updater in IdUpdaters)
            {
                var result = await updater.UpdateEventId(resultEvents);
                index++;
                var progress = (double)index / count;
                UpdateStep(reconciliationStep, $"Réconciliation: {Math.Round(progress * 100)}%", progress);
                logger.LogDebug("Updater {Type} finished: {Result}", updater.GetType().Name, result);
            }

            UpdateStep(reconciliationStep, "🤝 Réconciliation terminée ✅", 1.0, true);
            logger.LogInformation("✅ Event ID update completed");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur pendant la mise à jour des Event IDs");
        }
        finally
        {
            stopwatch.Stop();
            logger.LogInformation("✅ SendEventsAsync terminé en {Elapsed}ms", stopwatch.ElapsedMilliseconds);
            IsBusy = false;
        }
    }

    private SynchroStep CreateStep(string step, string description, double progress) =>
    new()
    {
        Step = step,
        Description = description,
        Progress = progress,
        IsCompleted = false
    };

    private void UpdateStep(SynchroStep step, string description, double progress, bool isCompleted = false)
    {
        step.Description = description;
        step.Progress = progress;
        step.IsCompleted = isCompleted;
    }
}