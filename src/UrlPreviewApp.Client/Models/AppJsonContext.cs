using System.Text.Json;
using System.Text.Json.Serialization;

namespace UrlPreviewApp.Client.Models;

/// <summary>
/// Source-generated serializer for the API contracts (trimming-safe in WebAssembly).
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(PageMetadata))]
[JsonSerializable(typeof(ApiError))]
[JsonSerializable(typeof(List<string>))]
public sealed partial class AppJsonContext : JsonSerializerContext;

/// <summary>
/// Same contracts, indented, for the "Raw" tab.
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, WriteIndented = true)]
[JsonSerializable(typeof(PageMetadata))]
public sealed partial class IndentedJsonContext : JsonSerializerContext;
