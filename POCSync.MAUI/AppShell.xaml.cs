using POCSync.MAUI.Views;

namespace POCSync.MAUI
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            Routing.RegisterRoute(nameof(SynchronisationPage), typeof(SynchronisationPage));
            Routing.RegisterRoute(nameof(PackageFormPage), typeof(PackageFormPage));
        }
    }
}
