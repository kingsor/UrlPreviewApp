using System.Text.Json;
using Microsoft.JSInterop;
using UrlPreviewApp.Client.Models;

namespace UrlPreviewApp.Client.Services;

/// <summary>
/// Most recently previewed URLs, kept in the browser's <c>localStorage</c>.
/// Call only after the first interactive render.
/// </summary>
public sealed class RecentUrlsStore(IJSRuntime jsRuntime)
{
    public const int MaxItems = 10;
    private const string StorageKey = "urlpreviewapp.recent-urls";

    public async Task<IReadOnlyList<string>> GetAsync()
    {
        try
        {
            var json = await jsRuntime.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            if (string.IsNullOrEmpty(json))
                return [];

            return JsonSerializer.Deserialize(json, AppJsonContext.Default.ListString) ?? [];
        }
        catch (Exception ex) when (ex is JSException or JsonException)
        {
            return [];
        }
    }

    public async Task<IReadOnlyList<string>> AddAsync(string url)
    {
        var updated = (await GetAsync())
            .Where(u => !string.Equals(u, url, StringComparison.OrdinalIgnoreCase))
            .Prepend(url)
            .Take(MaxItems)
            .ToList();

        await SaveAsync(updated);
        return updated;
    }

    public async Task ClearAsync()
    {
        try
        {
            await jsRuntime.InvokeVoidAsync("localStorage.removeItem", StorageKey);
        }
        catch (JSException)
        {
            // Storage unavailable (private mode, blocked site data): nothing to clear.
        }
    }

    private async Task SaveAsync(List<string> urls)
    {
        try
        {
            var json = JsonSerializer.Serialize(urls, AppJsonContext.Default.ListString);
            await jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
        }
        catch (JSException)
        {
            // Storage unavailable: recents simply are not persisted.
        }
    }
}
