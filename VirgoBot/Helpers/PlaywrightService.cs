using Microsoft.Playwright;

namespace VirgoBot.Helpers;

public class PlaywrightService : IAsyncDisposable
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    private async Task EnsureInitializedAsync()
    {
        if (_browser == null)
        {
            _playwright = await Playwright.CreateAsync();
            _browser = await _playwright.Chromium.LaunchAsync(new() { Headless = true });
        }
    }

    public async Task<string> NavigateAndGetContentAsync(string url)
    {
        await EnsureInitializedAsync();
        var page = await _browser!.NewPageAsync();
        try
        {
            await page.GotoAsync(url, new() { WaitUntil = WaitUntilState.NetworkIdle });
            return await page.ContentAsync();
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    public async Task<string> ClickAndGetContentAsync(string url, string selector)
    {
        await EnsureInitializedAsync();
        var page = await _browser!.NewPageAsync();
        try
        {
            await page.GotoAsync(url);
            await page.ClickAsync(selector);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            return await page.ContentAsync();
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    public async Task<string> FillFormAsync(string url, Dictionary<string, string> fields, string submitSelector)
    {
        await EnsureInitializedAsync();
        var page = await _browser!.NewPageAsync();
        try
        {
            await page.GotoAsync(url);
            foreach (var field in fields)
                await page.FillAsync(field.Key, field.Value);
            await page.ClickAsync(submitSelector);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            return await page.ContentAsync();
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    public async Task<byte[]> ScreenshotAsync(string url)
    {
        await EnsureInitializedAsync();
        var page = await _browser!.NewPageAsync();
        try
        {
            await page.GotoAsync(url);
            return await page.ScreenshotAsync();
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser != null) await _browser.DisposeAsync();
        _playwright?.Dispose();
    }
}
