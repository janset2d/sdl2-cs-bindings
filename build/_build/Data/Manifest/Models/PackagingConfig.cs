using System.Text.Json.Serialization;

namespace Build.Data.Manifest.Models;
public record PackagingConfig
{
    [JsonPropertyName("validation_mode")]
    [JsonConverter(typeof(JsonStringEnumConverter<ValidationMode>))]
    public required ValidationMode ValidationMode { get; init; }
    [JsonPropertyName("core_library")] public required string CoreLibrary { get; init; }
}
