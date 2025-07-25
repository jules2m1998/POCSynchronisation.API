using CommunityToolkit.Mvvm.ComponentModel;

namespace POCSync.MAUI.Models;

public partial class SynchroStep : ObservableObject
{
    [ObservableProperty]
    string step = string.Empty;

    [ObservableProperty]
    string description = string.Empty;

    [ObservableProperty]
    bool isCompleted = false;

    [ObservableProperty]
    double progress = 0.0;
}
