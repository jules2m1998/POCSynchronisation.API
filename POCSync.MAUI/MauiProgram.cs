﻿using Dapper;
using Infrastructure.Dapper;
using Infrastructure.Dapper.Abstractions;
using Infrastructure.Dapper.TypeHandlers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Poc.Synchronisation.Application;
using Poc.Synchronisation.Domain.Abstractions;
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
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenssSansSemibold");
                });
            builder.AddConfiguration();
#if DEBUG
            builder.Logging.AddDebug();
#endif
            SQLitePCL.Batteries_V2.Init();

            var dbName = builder.Configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
            var dbPwd = builder.Configuration["DbPwd"] ?? string.Empty;
            var apiUrl = builder.Configuration["ApiUrl"] ?? string.Empty;


            builder.Services
                .AddApplication();

            string dbPath;
            dbPath = Path.Combine(FileSystem.AppDataDirectory, dbName); // fallback
#if ANDROID
            string downloadsDir = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDocuments)?.AbsolutePath ?? FileSystem.AppDataDirectory;
            dbPath = Path.Combine(downloadsDir, dbName);
#else
            dbPath = Path.Combine(FileSystem.AppDataDirectory, dbName); // fallback
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

            var _ = builder.Services.AddInfrastructure(dbPath, dbPwd, apiUrl).GetAwaiter().GetResult();

            return builder.Build();
        }
    }
}
