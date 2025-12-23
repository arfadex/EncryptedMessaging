using System.Text.Json;

namespace EncryptedMessaging.Client;

public static class Config
{
    private const string DefaultServerUrl = "http://localhost:5000";
    
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "EncryptedMessaging"
    );
    private static readonly string ConfigFile = Path.Combine(ConfigDir, "config.json");

    public static string ServerUrl { get; private set; } = DefaultServerUrl;
    public static string WsUrl => ServerUrl.Replace("https://", "wss://").Replace("http://", "ws://") + "/ws";

    private record ConfigData(string ServerUrl);

    static Config()
    {
        Load();
    }

    public static void SetServerUrl(string url)
    {
        url = url.TrimEnd('/');
        if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            url = "https://" + url;
        ServerUrl = url;
        Save();
    }

    public static void ResetServerUrl()
    {
        ServerUrl = DefaultServerUrl;
        Save();
    }

    private static void Load()
    {
        try
        {
            if (!File.Exists(ConfigFile))
                return;

            var json = File.ReadAllText(ConfigFile);
            var config = JsonSerializer.Deserialize<ConfigData>(json);
            
            if (config != null && !string.IsNullOrEmpty(config.ServerUrl))
                ServerUrl = config.ServerUrl;
        }
        catch
        {
        }
    }

    private static void Save()
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            var config = new ConfigData(ServerUrl);
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigFile, json);
        }
        catch
        {
        }
    }
}
