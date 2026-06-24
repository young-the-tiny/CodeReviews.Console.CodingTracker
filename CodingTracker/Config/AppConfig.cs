using Microsoft.Extensions.Configuration;

namespace CodingTracker.Config;


internal static class AppConfig
{
    public static string ConnectionString { get; }
    public static string DateFormat { get; }

    static AppConfig()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json")
            .Build();

        ConnectionString = config.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' not found in appsettings.json.");
        DateFormat = config["DateFormat"] ?? "yyyy-MM-dd HH:mm";
    }
}