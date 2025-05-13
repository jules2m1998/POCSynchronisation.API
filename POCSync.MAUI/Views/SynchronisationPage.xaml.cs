using POCSync.MAUI.ViewModels;

namespace POCSync.MAUI.Views;

public partial class SynchronisationPage : ContentPage
{
    public SynchronisationPage(SynchronisationViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Call the initialization command
        if (BindingContext is SynchronisationViewModel viewModel)
        {
            viewModel.InitialisationCommand.Execute(null);
        }
    }
}