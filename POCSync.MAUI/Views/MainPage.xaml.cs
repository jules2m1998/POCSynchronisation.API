using POCSync.MAUI.ViewModels;

namespace POCSync.MAUI.Views;

public partial class MainPage : ContentPage
{
    public MainPage(PackageListViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Call the initialization command
        if (BindingContext is PackageListViewModel viewModel)
        {
            viewModel.LoadPackagesCommand.Execute(null);
        }
    }
}