using Poc.Synchronisation.Domain.Abstractions.Services;
using POCSync.MAUI.ViewModels;
using POCSync.MAUI.Views;

namespace POCSync.MAUI
{
    public partial class App : Application
    {
        private readonly IAppGuards _guards;
        private readonly SynchronisationViewModel _synchronisationViewModel;

        public App(IAppGuards guards, SynchronisationViewModel vm)
        {
            _guards = guards;
            _synchronisationViewModel = vm;
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            if (_guards.DoesDbInitialized())
            {
                return new Window(new AppShell());
            }

            return new Window(new SynchronisationPage(_synchronisationViewModel));
        }
    }
}