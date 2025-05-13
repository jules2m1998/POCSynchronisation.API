using Infrastructure.Dapper;
using System.Threading.Tasks;

namespace POCSync.MAUI
{
    public partial class App : Application
    {
        private readonly DatabaseInitializer _databaseInitializer;

        public App(DatabaseInitializer databaseInitializer)
        {
            _databaseInitializer = databaseInitializer;
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }

        protected override async void OnStart()
        {
            base.OnStart();

            await _databaseInitializer.InitializeDatabaseAsync();
        }
    }
}