namespace VirgoBot.Configuration;

public static class AppConstants
{
    public const string DefaultListenUrl = "http://localhost:5000/";
    public const int WebSocketBufferSize = 4096;
    public const int DefaultMaxTokens = 8192;
    public const int DefaultMessageLimit = 20;
    public const string ConfigDirectory = "config";
    public const string WorkspaceDirectory = "workspace";
    public const string ConfigFileName = "config.json";
}
