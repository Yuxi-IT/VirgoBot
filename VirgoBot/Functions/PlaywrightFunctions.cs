using System.Text.Json;
using VirgoBot.Services;

namespace VirgoBot.Functions;

public static class PlaywrightFunctions
{
    public static IEnumerable<FunctionDefinition> Register(PlaywrightService playwrightService)
    {
        yield return new FunctionDefinition("playwright_navigate", "使用浏览器访问网页并获取完整HTML内容", new
        {
            type = "object",
            properties = new
            {
                url = new { type = "string", description = "要访问的网址" }
            },
            required = new[] { "url" }
        }, async input =>
        {
            var url = input.GetProperty("url").GetString() ?? "";
            return await playwrightService.NavigateAndGetContentAsync(url);
        });

        yield return new FunctionDefinition("playwright_click", "点击网页元素并获取结果", new
        {
            type = "object",
            properties = new
            {
                url = new { type = "string", description = "网址" },
                selector = new { type = "string", description = "CSS选择器" }
            },
            required = new[] { "url", "selector" }
        }, async input =>
        {
            var url = input.GetProperty("url").GetString() ?? "";
            var selector = input.GetProperty("selector").GetString() ?? "";
            return await playwrightService.ClickAndGetContentAsync(url, selector);
        });

        yield return new FunctionDefinition("playwright_fill_form", "填写表单并提交", new
        {
            type = "object",
            properties = new
            {
                url = new { type = "string", description = "网址" },
                fields = new { type = "object", description = "字段映射(选择器:值)" },
                submit_selector = new { type = "string", description = "提交按钮选择器" }
            },
            required = new[] { "url", "fields", "submit_selector" }
        }, async input =>
        {
            var url = input.GetProperty("url").GetString() ?? "";
            var submitSelector = input.GetProperty("submit_selector").GetString() ?? "";
            var fields = new Dictionary<string, string>();
            foreach (var prop in input.GetProperty("fields").EnumerateObject())
                fields[prop.Name] = prop.Value.GetString() ?? "";
            return await playwrightService.FillFormAsync(url, fields, submitSelector);
        });

        yield return new FunctionDefinition("playwright_screenshot", "截取网页截图", new
        {
            type = "object",
            properties = new
            {
                url = new { type = "string", description = "网址" },
                save_path = new { type = "string", description = "保存路径" }
            },
            required = new[] { "url", "save_path" }
        }, async input =>
        {
            var url = input.GetProperty("url").GetString() ?? "";
            var savePath = input.GetProperty("save_path").GetString() ?? "";
            var screenshot = await playwrightService.ScreenshotAsync(url);
            await File.WriteAllBytesAsync(savePath, screenshot);
            return $"截图已保存到: {savePath}";
        });
    }
}
