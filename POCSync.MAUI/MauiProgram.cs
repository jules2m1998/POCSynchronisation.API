using Infrastructure.Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Poc.Synchronisation.Application;

namespace POCSync.MAUI
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });
            builder.AddConfiguration();
#if DEBUG
    		builder.Logging.AddDebug();
#endif
            SQLitePCL.Batteries_V2.Init();

            var dbName = builder.Configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
            var dbPwd = builder.Configuration["DbPwd"] ?? string.Empty;


            builder.Services
                .AddApplication();
            var dbPath = Path.Combine(FileSystem.AppDataDirectory, dbName);
            var _ = builder.Services.AddInfrastructure(dbPath, dbPwd).GetAwaiter().GetResult();

            builder.Services.AddTransient<MainPage>();

            return builder.Build();
        }
    }
}
