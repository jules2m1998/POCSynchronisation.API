using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace POCSync.MAUI.Models;

public partial class SynchroStep : ObservableObject
{
    public SynchroStep()
    {
        Errors.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasErrors));
    }

    [ObservableProperty]
    string step = string.Empty;

    [ObservableProperty]
    string description = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCompleted))]
    bool isSuccess = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCompleted))]
    bool isError = false;

    [ObservableProperty]
    double progress = 0.0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrors))]
    private ObservableCollection<string> errors = [];

    public bool HasErrors => Errors.Any();

    public virtual bool IsCompleted => IsSuccess || IsError;
}
