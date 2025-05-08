using POCSync.MAUI.ViewModels;

namespace POCSync.MAUI.Views;

public partial class SynchronisationPage : ContentPage
{
	public SynchronisationPage(SynchronisationViewModel vm)
	{
		InitializeComponent();
		BindingContext = vm;
	}
}