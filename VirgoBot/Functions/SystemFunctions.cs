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

        yield return new FunctionDefinition("get_time", "获取当前服务器时间",
            new { type = "object", properties = new { } },
            _ => Task.FromResult(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));

        yield return new FunctionDefinition("get_workspace", "获取工作目录路径",
            new { type = "object", properties = new { } },
            _ => Task.FromResult(workspace));

        yield return new FunctionDefinition("get_specialfolder", "获取特殊目录路径(桌面,文档,音乐等)",
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
