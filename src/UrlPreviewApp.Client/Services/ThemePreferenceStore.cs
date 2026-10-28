using Microsoft.JSInterop;

namespace UrlPreviewApp.Client.Services;

/// <summary>
/// The user's light/dark choice, kept in the browser's <c>localStorage</c>.
/// Call only after the first interactive render.
/// </summary>
public sealed class ThemePreferenceStore(IJSRuntime jsRuntime)
{
    private const string StorageKey = "urlpreviewapp.dark-mode";

    /// <summary>The saved choice, or <c>null</c> when the user never picked one.</summary>
    public async Task<bool?> GetAsync()
    {
        try
        {
            return await jsRuntime.InvokeAsync<string?>("localStorage.getItem", StorageKey) switch
            {
                "true" => true,
                "false" => false,
                _ => null,
            };
        }
        catch (JSException)
        {
            return null;
        }
    }

    public async Task SetAsync(bool isDarkMode)
    {
        try
        {
            await jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey, isDarkMode ? "true" : "false");
        }
        catch (JSException)
        {
            // Storage unavailable: the choice lasts for this session only.
        }
    }
}
