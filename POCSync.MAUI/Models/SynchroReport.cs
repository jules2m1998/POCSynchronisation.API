using CommunityToolkit.Mvvm.ComponentModel;

namespace POCSync.MAUI.Models;

public partial class SynchroReport : ObservableObject
{
    [ObservableProperty]
    private int totalEventToSync = 0;

    [ObservableProperty]
    private int totalEventSynced = 0;

    [ObservableProperty]
    private int totalEventToApply = 0;

    [ObservableProperty]
    private int totalEventApplied = 0;

    [ObservableProperty]
    private int totalConflict = 0;

    [ObservableProperty]
    private int totalDocumentToSync = 0;

    [ObservableProperty]
    private int totalDocumentSynced = 0;

    [ObservableProperty]
    private bool isvisible = false;
}
