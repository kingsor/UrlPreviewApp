# Container image for UrlPreviewApp (used by DigitalOcean App Platform, see deploy.md).
# Build from the repository root:  docker build -t urlpreviewapp .
# UrlPreviewParser comes from NuGet (UseLocalUrlPreviewParser=false), no sibling repo needed.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Restore first, so the layer is cached until a project file changes.
COPY Directory.Build.props UrlPreviewApp.slnx ./
COPY src/UrlPreviewApp/UrlPreviewApp.csproj src/UrlPreviewApp/
COPY src/UrlPreviewApp.Client/UrlPreviewApp.Client.csproj src/UrlPreviewApp.Client/
RUN dotnet restore src/UrlPreviewApp/UrlPreviewApp.csproj

COPY src/ src/
RUN dotnet publish src/UrlPreviewApp/UrlPreviewApp.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .

# The .NET images listen on 8080 (ASPNETCORE_HTTP_PORTS) and provide a non-root "app" user.
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "UrlPreviewApp.dll"]
