using System.Text.Json;
using System.Text.Json.Serialization;
using NuGet.Versioning;

namespace Build.Data.Versions;
internal sealed class PackageFamilyVersionSetJsonConverter : JsonConverter<PackageFamilyVersionSet>
{
    public override PackageFamilyVersionSet Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected start of object for PackageFamilyVersionSet.");
        }

        var entries = new List<PackageFamilyVersion>();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                try
                {
                    return new PackageFamilyVersionSet(entries);
                }
                catch (ArgumentException ex)
                {
                    throw new JsonException(ex.Message, ex);
                }
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException($"Expected property name; got {reader.TokenType}.");
            }

            var rawFamily = reader.GetString() ?? throw new JsonException("Family identifier cannot be null.");

            PackageFamilyId family;

            try
            {
                family = new PackageFamilyId(rawFamily);
            }
            catch (ArgumentException ex)
            {
                throw new JsonException(ex.Message, ex);
            }

            if (!reader.Read() || reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException($"Expected string version value for family '{rawFamily}'; got {reader.TokenType}.");
            }

            var rawVersion = reader.GetString();

            if (!NuGetVersion.TryParse(rawVersion, out var version))
            {
                throw new JsonException($"Invalid NuGetVersion '{rawVersion}' for family '{rawFamily}'.");
            }

            entries.Add(new PackageFamilyVersion(family, version));
        }

        throw new JsonException("Unexpected end of input while reading PackageFamilyVersionSet.");
    }

    public override void Write(Utf8JsonWriter writer, PackageFamilyVersionSet value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);

        writer.WriteStartObject();

        foreach (var family in value.Families)
        {
            writer.WriteString(family.Value, value.RequireVersion(family).ToNormalizedString());
        }

        writer.WriteEndObject();
    }
}
