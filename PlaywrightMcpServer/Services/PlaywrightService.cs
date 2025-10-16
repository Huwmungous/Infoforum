using Microsoft.Playwright;
using PlaywrightMcpServer.Models;
using Microsoft.Extensions.Logging;

namespace PlaywrightMcpServer.Services;

public class PlaywrightService : IAsyncDisposable
{
    private readonly ILogger<PlaywrightService> _logger;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IPage? _page;

    public PlaywrightService(ILogger<PlaywrightService> logger)
    {
        _logger = logger;
    }

    private async Task EnsureInitializedAsync()
    {
        if (_playwright == null)
        {
            _playwright = await Playwright.CreateAsync();
            _browser = await _playwright.Chromium.LaunchAsync(new() { Headless = true });
            _page = await _browser.NewPageAsync();
        }
    }

    public async Task<NavigationResult> NavigateAsync(string url, int? timeout = null)
    {
        try
        {
            await EnsureInitializedAsync();
            
            var options = new PageGotoOptions { Timeout = timeout ?? 30000 };
            var response = await _page!.GotoAsync(url, options);

            var title = await _page.TitleAsync();
            var content = await _page.ContentAsync();

            return new NavigationResult
            {
                Success = response?.Ok ?? false,
                Url = url,
                Title = title,
                Content = content
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Navigation error");
            return new NavigationResult { Success = false, Error = ex.Message };
        }
    }

    public async Task<ScreenshotResult> ScreenshotAsync(string? path = null, bool fullPage = false)
    {
        try
        {
            if (_page == null)
            {
                return new ScreenshotResult { Success = false, Error = "Page not initialized" };
            }

            var options = new PageScreenshotOptions { FullPage = fullPage };
            
            if (!string.IsNullOrEmpty(path))
            {
                options.Path = path;
                await _page.ScreenshotAsync(options);
                return new ScreenshotResult { Success = true, FilePath = path };
            }
            else
            {
                var bytes = await _page.ScreenshotAsync(options);
                var base64 = Convert.ToBase64String(bytes);
                return new ScreenshotResult { Success = true, Base64Image = base64 };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Screenshot error");
            return new ScreenshotResult { Success = false, Error = ex.Message };
        }
    }

    public async Task<EvaluationResult> EvaluateAsync(string script)
    {
        try
        {
            if (_page == null)
            {
                return new EvaluationResult { Success = false, Error = "Page not initialized" };
            }

            var result = await _page.EvaluateAsync<object>(script);
            return new EvaluationResult { Success = true, Data = result };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Evaluation error");
            return new EvaluationResult { Success = false, Error = ex.Message };
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_page != null) await _page.CloseAsync();
        if (_browser != null) await _browser.CloseAsync();
        _playwright?.Dispose();
    }
}