using System.Text;
using System.Text.Json;
using VirgoBot.Configuration;

namespace VirgoBot.Functions;

public static class SystemFunctions
{
    public static IEnumerable<FunctionDefinition> Register()
    {
        var workspace = Path.Combine(Environment.CurrentDirectory, AppConstants.WorkspaceDirectory);
        if (!Directory.Exists(workspace)) Directory.CreateDirectory(workspace);

        yield return new FunctionDefinition("get_time", "Get current server time",
            new { type = "object", properties = new { } },
            _ => Task.FromResult(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));

        yield return new FunctionDefinition("get_workspace", "Get workspace directory path",
            new { type = "object", properties = new { } },
            _ => Task.FromResult(workspace));

        yield return new FunctionDefinition("get_specialfolder", "Get special folder paths (Desktop, Documents, Music, etc.)",
            new { type = "object", properties = new { } },
            async _ =>
            {
                var folders = new[]
                {
                    Environment.SpecialFolder.Desktop,
                    Environment.SpecialFolder.MyDocuments,
                    Environment.SpecialFolder.MyMusic,
                    Environment.SpecialFolder.MyPictures,
                    Environment.SpecialFolder.MyVideos,
                    Environment.SpecialFolder.ApplicationData,
                    Environment.SpecialFolder.LocalApplicationData,
                    Environment.SpecialFolder.ProgramFiles,
                };

                var sb = new StringBuilder();
                foreach (var folder in folders)
                {
                    string path = Environment.GetFolderPath(folder);
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        sb.AppendLine(path);
                    }
                }
                return sb.ToString();
            });
    }
}
