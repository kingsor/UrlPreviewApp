using Microsoft.JSInterop;

namespace UrlPreviewApp.Client.Services;

public sealed record ImageSize(int Width, int Height);

/// <summary>
/// Browser APIs used by the UI (clipboard, image dimensions, keyboard shortcuts).
/// Call only after the first interactive render.
/// </summary>
public sealed class BrowserInterop(IJSRuntime jsRuntime) : IAsyncDisposable
{
    private IJSObjectReference? _module;

    public async Task<bool> CopyTextAsync(string text)
    {
        try
        {
            var module = await GetModuleAsync();
            return await module.InvokeAsync<bool>("copyText", text);
        }
        catch (JSException)
        {
            return false;
        }
    }

    public async Task<ImageSize?> GetImageSizeAsync(string url)
    {
        try
        {
            var module = await GetModuleAsync();
            return await module.InvokeAsync<ImageSize?>("getImageSize", url);
        }
        catch (JSException)
        {
            return null;
        }
    }

    /// <summary>
    /// Registers the global <c>/</c> (focus input) and <c>Escape</c> (clear) shortcuts.
    /// Dispose the returned handle to unregister them.
    /// </summary>
    public async Task<IAsyncDisposable> RegisterShortcutsAsync<T>(DotNetObjectReference<T> target)
        where T : class
    {
        var module = await GetModuleAsync();
        var registration = await module.InvokeAsync<IJSObjectReference>("registerShortcuts", target);
        return new ShortcutRegistration(registration);
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // The page is going away.
            }
        }
    }

    private async Task<IJSObjectReference> GetModuleAsync() =>
        _module ??= await jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/app.js");

    private sealed class ShortcutRegistration(IJSObjectReference registration) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await registration.InvokeVoidAsync("dispose");
                await registration.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // The page is going away.
            }
        }
    }
}
