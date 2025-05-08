using Dapper;
using Microsoft.Extensions.Configuration;
using System.Reflection;

namespace POCSync.MAUI;

public static class DependencyInjection
{
    public static MauiAppBuilder AddConfiguration(this MauiAppBuilder builder)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("POCSync.MAUI.appsettings.json");
        if (stream is null)
        {
            throw new InvalidOperationException("Could not find appsettings.json in the assembly.");
        }
        var config = new ConfigurationBuilder().AddJsonStream(stream).Build();
        builder.Configuration.AddConfiguration(config);

        return builder;
    }
}
