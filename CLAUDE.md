# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
dotnet run --project src/UrlPreviewApp            # https://localhost:7110 (deep link: /?url=github.com)
dotnet build UrlPreviewApp.slnx
dotnet test UrlPreviewApp.slnx
dotnet test UrlPreviewApp.slnx --filter "FullyQualifiedName~NetworkGuardTests"   # one class
dotnet test UrlPreviewApp.slnx --filter "FullyQualifiedName~UrlNormalizerTests.TryNormalize_AcceptsWebUrls"  # one test
dotnet build -p:UseLocalUrlPreviewParser=true      # build against ../UrlPreviewParser sources
```

.NET 10, solution file is `UrlPreviewApp.slnx`. UI components are **MudBlazor** (package referenced by the Client only; services registered in `AddUrlPreviewBrowserServices()`, CSS/JS linked in `App.razor`). Theme lives in `Client/Layout/AppTheme.cs` (light + dark palettes); the light/dark toggle is in `MainLayout.razor`, persisted by `ThemePreferenceStore` (localStorage, falls back to the system preference). `src/UrlPreviewApp/wwwroot/app.css` holds only custom styling (platform previews, score ring, loader, small tweaks); its `:root` tokens alias `--mud-palette-*` so they follow the theme. There is no Node/Tailwind toolchain.

## UrlPreviewParser reference

`Directory.Build.props` switches how the server references the UrlPreviewParser library:
- `UseLocalUrlPreviewParser=false` (actual default in the props file): NuGet `UrlPreviewParser` `$(UrlPreviewParserVersion)` (1.1.0).
- `true`: project reference to `..\UrlPreviewParser\src\UrlPreviewParser` (sibling repo).

The README claims the default is `true`; the props file is authoritative.

## Architecture

Blazor Web App, render mode **InteractiveWebAssembly with prerendering** (set on `Routes`/`HeadOutlet` in `src/UrlPreviewApp/Components/App.razor`).

- `src/UrlPreviewApp` — ASP.NET Core host. Exposes `GET /api/metadata?url=…` (`Endpoints/MetadataEndpoint.cs`). **The only project that references UrlPreviewParser** (keeps AngleSharp out of the WASM bundle) — never add that reference to the Client.
- `src/UrlPreviewApp.Client` — WASM UI (`Pages/`, `Components/`, `Layout/`), pure logic in `Logic/` (score, per-platform preview rules, HTML tag generator, URL normalization), and the API contracts in `Models/` (`PageMetadata`, `ApiError`, `MetadataLookupResult`).
- `tests/UrlPreviewApp.Tests` — xUnit; references only the server project (which pulls in the Client). `InternalsVisibleTo` is set on the server.

### Request flow / the `IPageMetadataProvider` split

`IPageMetadataProvider` (Client) has two implementations, registered per host:
- Browser: `MetadataApiClient` calls `api/metadata` over HTTP (`Client/Program.cs`).
- Server: `MetadataService` (`src/UrlPreviewApp/Services`) is used directly during prerendering and also backs the endpoint.

`MetadataService` → `UrlNormalizer.TryNormalize` → `UrlPreviewClient.GetPreviewAsync` → `MetadataMapper.Map` (flattens parser result, drops raw HTML) → `SiteProbe.ApplyAsync` (parallel HEAD/GET for robots.txt, sitemap.xml, /favicon.ico fallback). Exceptions are translated into user-facing messages + status codes (400 invalid/blocked, 502 upstream, 504 timeout, 500 other).

Browser-only services (`BrowserInterop` → `wwwroot/js/app.js` ES module, `RecentUrlsStore` → localStorage) are registered on **both** hosts via `AddUrlPreviewBrowserServices()` so prerendering can resolve them, but must only be called after the first interactive render (`OnAfterRenderAsync(firstRender)`), as `Pages/Home.razor` does.

### Conventions that span files

- JSON uses source-generated contexts (`Client/Models/AppJsonContext.cs`, trimming-safe for WASM). Any new type crossing the API or localStorage must be added as `[JsonSerializable]` there; the server registers the context via `ConfigureHttpJsonOptions` in `Program.cs`.
- Platform previews are data-driven: one `PlatformPreview` component + `PlatformThemes.All` in `Logic/Platforms.cs` (layout, line clamps, site-label source, `PreferTwitterTags`) with colors in CSS classes `preview--<platform>`. Add a platform by extending the `Platform` enum, `PlatformThemes.All`, and the CSS — not by adding a component.
- Links taken from analyzed pages are rendered as `<a>` only if `UrlNormalizer.IsHttpUrl`.

### Security (server)

- SSRF guard: the named `HttpClient` (`MetadataService.HttpClientName` = "preview") uses `NetworkGuard.ConnectAsync` as `SocketsHttpHandler.ConnectCallback`, checking the resolved IP on every connection including redirects; proxy disabled, max 5 redirects. `NetworkGuard.IsPublic` holds the blocked ranges. All outbound fetches to analyzed sites must go through this client.
- Fixed-window rate limit per client IP on `/api/metadata` (policy `MetadataEndpoint.RateLimitPolicy`).
- Config section `Preview` → `PreviewOptions` (timeouts, `MaxContentBytes`, `RequestsPerMinute`, `UserAgent`). `AllowPrivateNetworks=true` disables the SSRF guard — local debugging only.

### Tests

- `LogicTests.cs`: Client logic and JSON contract round-trip.
- `ServerTests.cs`: `MetadataMapper`, `NetworkGuard.IsPublic`, `MetadataService` with a stubbed `HttpMessageHandler`/`IHttpClientFactory` (no network), and endpoint tests via `WebApplicationFactory<Program>` (`Program` is exposed with `public partial class Program`).
