# UrlPreviewApp

See how your links appear on social platforms (Telegram, Discord, Slack, X, Facebook, LinkedIn,
WhatsApp), with a metadata quality score and Open Graph / X Card / image inspection.

A .NET 10 Blazor WebAssembly port of [linkpreview](https://github.com/KartikLabhshetwar/linkpreview),
built on the [UrlPreviewParser](../UrlPreviewParser) library.

## Run

```bash
dotnet run --project src/UrlPreviewApp
```

Open https://localhost:7110. Deep links work too: `https://localhost:7110/?url=github.com`.

```bash
dotnet test UrlPreviewApp.slnx
```

## Architecture

Blazor Web App, render mode **Interactive WebAssembly** (with prerendering).

| Project | Role |
|---|---|
| `src/UrlPreviewApp` | ASP.NET Core host: `GET /api/metadata?url=…`, anti-SSRF HTTP client, rate limiting. The only project that references **UrlPreviewParser**. |
| `src/UrlPreviewApp.Client` | WebAssembly UI (pages, components, JS interop) and pure logic: score, per-platform preview rules, HTML tag generator, URL normalization. Also holds the API contracts (`PageMetadata`, `ApiError`). |
| `tests/UrlPreviewApp.Tests` | xUnit: logic, mapper, service (stubbed HTTP), endpoint (`WebApplicationFactory`). |

A browser cannot download third-party pages (CORS), so scraping happens on the server:
`MetadataService` calls `UrlPreviewClient.GetPreviewAsync`, `MetadataMapper` flattens the result
(without the raw HTML), and `SiteProbe` checks `robots.txt`, `sitemap.xml` and the `/favicon.ico` fallback.
AngleSharp therefore never reaches the WebAssembly bundle.

### Security

- **SSRF**: the outgoing `HttpClient` uses `NetworkGuard.ConnectAsync` as `SocketsHttpHandler.ConnectCallback`.
  It resolves DNS and connects only to public IPs (loopback, private, link-local/cloud metadata, CGNAT,
  ULA, multicast… are refused), for every connection including redirects. The proxy is disabled.
- Timeouts, a maximum body size and a maximum of 5 redirects.
- Fixed-window rate limit per client IP on `/api/metadata`.
- Links taken from the analyzed page are rendered as `<a>` only when they are `http(s)`.

## Configuration (`appsettings.json`, section `Preview`)

| Key | Default | |
|---|---|---|
| `AllowPrivateNetworks` | `false` | `true` disables the SSRF guard. Never enable it on a deployed instance. |
| `TimeoutSeconds` | `15` | Page fetch timeout. |
| `MaxContentBytes` | `5242880` | Bytes read from a page (metadata is in `<head>`). |
| `SiteChecksTimeoutSeconds` | `5` | robots.txt / sitemap.xml / favicon.ico checks. |
| `RequestsPerMinute` | `30` | Rate limit per client IP. |
| `UserAgent` | `Mozilla/5.0 (compatible; UrlPreviewApp/1.0; …)` | Sent to analyzed sites. |

## UrlPreviewParser: local sources or NuGet

`Directory.Build.props` selects how the library is referenced:

- `UseLocalUrlPreviewParser=true` (default): project reference to `..\UrlPreviewParser\src\UrlPreviewParser`.
  Version 1.1.0 is required (new typed tags, URL resolution, options).
- `UseLocalUrlPreviewParser=false`: `PackageReference` to `UrlPreviewParser` `$(UrlPreviewParserVersion)`.

```bash
dotnet build -p:UseLocalUrlPreviewParser=false
```

## Differences from the original

- Previews: one parametric component (`PlatformPreview` + `PlatformThemes`) instead of seven copies;
  X prefers `twitter:*` tags and shows the domain.
- Keyboard shortcuts no longer block typing: `/` focuses the input only outside text fields, `Escape` clears.
  The number keys and `Ctrl+R` are no longer intercepted.
- The Raw HTML tab rebuilds tags from the real `<head>` (`property=` kept, no duplicates).
- Image dimensions: green = exact, yellow = same aspect ratio, red = different.
- Error messages report the real cause (HTTP status, timeout, blocked address).
- No analytics script and no GitHub star counter.
- Styling: plain CSS with the original dark theme tokens (no Tailwind/Node toolchain).

## License and credits

- UI and features ported from [linkpreview](https://github.com/KartikLabhshetwar/linkpreview),
  MIT License, Copyright (c) 2026 Kartik Labhshetwar. The logo (`wwwroot/icon.svg`) and the loader animation come from that project.
- Icons: [Lucide](https://lucide.dev), ISC License.

```
MIT License

Copyright (c) 2026 Kartik Labhshetwar

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```
