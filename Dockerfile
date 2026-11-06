# Container image for UrlPreviewApp (used by DigitalOcean App Platform, see deploy.md).
# Build from the repository root:  docker build -t urlpreviewapp .
# UrlPreviewParser comes from NuGet (UseLocalUrlPreviewParser=false), no sibling repo needed.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Restore first, so the NuGet packages are cached in a layer until a project file changes.
COPY Directory.Build.props UrlPreviewApp.slnx ./
COPY src/UrlPreviewApp/UrlPreviewApp.csproj src/UrlPreviewApp/
COPY src/UrlPreviewApp.Client/UrlPreviewApp.Client.csproj src/UrlPreviewApp.Client/
RUN dotnet restore src/UrlPreviewApp/UrlPreviewApp.csproj

COPY src/ src/
# No --no-restore here: the SDK adds the implicit Microsoft.AspNetCore.App.Internal.Assets package
# (which provides _framework/blazor.web.js) only when it sees the Razor components, i.e. with the
# sources present. Restoring again is fast (packages are cached) and picks it up.
RUN dotnet publish src/UrlPreviewApp/UrlPreviewApp.csproj -c Release -o /app

# Fail the build if the Blazor Web script is missing: without it the page never becomes interactive.
RUN grep -q '"_framework/blazor.web' /app/UrlPreviewApp.staticwebassets.endpoints.json \
    || (echo "ERROR: _framework/blazor.web.js missing from the publish output" && exit 1)

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .

# The .NET images listen on 8080 (ASPNETCORE_HTTP_PORTS) and provide a non-root "app" user.
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "UrlPreviewApp.dll"]
