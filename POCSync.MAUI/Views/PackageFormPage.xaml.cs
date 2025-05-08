using POCSync.MAUI.ViewModels;

namespace POCSync.MAUI.Views;

public partial class PackageFormPage : ContentPage
{
	public PackageFormPage(PackageFormViewModel vm)
	{
		InitializeComponent();
		BindingContext = vm;
	}
}