using CommunityToolkit.Maui;
using Dapper;
using Infrastructure.Dapper;
using Infrastructure.Dapper.Abstractions;
using Infrastructure.Dapper.Services;
using Infrastructure.Dapper.TypeHandlers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Poc.Synchronisation.Application;
using Poc.Synchronisation.Domain.Abstractions;
using Poc.Synchronisation.Domain.Abstractions.Services;
using POCSync.MAUI.Services;
using POCSync.MAUI.Services.Abstractions;
using POCSync.MAUI.Tools;
using POCSync.MAUI.ViewModels;
using POCSync.MAUI.Views;

namespace POCSync.MAUI
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", alias: "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", alias: "OpenSansSemibold");
                    fonts.AddFont("FontAwesome-Regular-400.otf", alias: "FontAwesome");
                    fonts.AddFont("FontAwesome-Solid-900.otf", alias: "FontAwesomeSolid");
                });
            builder.AddConfiguration();
#if DEBUG
            builder.Logging.AddDebug();
#endif

            var dbName = builder.Configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
            var dbPwd = builder.Configuration["DbPwd"] ?? string.Empty;
            var apiUrl = builder.Configuration["ApiUrl"] ?? string.Empty;


            var basePath = builder.Configuration.GetValue<string>("DocumentStorage:BasePath")?.Replace("wwwroot/", "");
            var defaultSubFolder = builder.Configuration.GetValue<string>("DocumentStorage:DefaultSubFolder");


            builder.Services
                .AddApplication();

            string dbPath;
            dbPath = Path.Combine(FileSystem.AppDataDirectory, dbName); // fallback
#if ANDROID
            string downloadsDir = PermisionManager.GetStoragePath();
            dbPath = Path.Combine(downloadsDir, dbName);
#endif
            SqlMapper.AddTypeHandler(new SqliteGuidTypeHandler());


            builder.Services.AddTransient<PackageListViewModel>();

            builder.Services.AddTransient<SynchronisationViewModel>();
            builder.Services.AddTransient<SynchronisationPage>();

            builder.Services.AddTransient<PackageFormViewModel>();
            builder.Services.AddTransient<PackageFormPage>();

            builder.Services.AddScoped<IPackageService, PackageService>();
            builder.Services.AddScoped<IPermissionManger, PermisionManager>();
            builder.Services.AddTransient<IPlatformIdentifier, PlatformIdentifier>();
            builder.Services.AddTransient<IAppGuards, AppGuards>();


            builder.Services.AddScoped<IFileSystemPath, FileSystemPath>();
            builder.Services.AddScoped<IDocumentService, DocumentService>();
            builder.Services.AddScoped<IFileTransferService, DocumentService>();
            builder.Services.AddScoped<IPackageImageService, PackageImageService>();
            builder.Services.AddTransient<FileManager>();
            builder.Services.AddHttpClient("FileDownloadClient", client =>
            {
                var baseAddress = $"{apiUrl}/{basePath}/{defaultSubFolder}/";
                client.BaseAddress = new Uri(baseAddress);
            })
#if DEBUG
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
                });
#else
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler());
#endif


            builder.Services.AddScoped<IDBForeignKeyMode, DBForeignKeyMode>();
            builder.Services.AddScoped<SynchronisationService>();

            var _ = builder.Services.AddInfrastructure(dbPath, dbPwd, apiUrl).GetAwaiter().GetResult();

            return builder.Build();
        }
    }
}
