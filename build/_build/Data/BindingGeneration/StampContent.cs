using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Build.Host.Cake;
using Cake.Core;

namespace Build.Data.BindingGeneration;

internal static class StampContent
{
    public static string Normalize(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    public static string Hash(string text) => "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Normalize(text))));

    public static string BlobHash(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(Normalize(text));
        var prefix = Encoding.UTF8.GetBytes("blob " + bytes.Length.ToString(CultureInfo.InvariantCulture) + "\0");
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
        hash.AppendData(prefix);
        hash.AppendData(bytes);
        return "git-sha1:" + Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    public static string Canonical<T>(ICakeContext context, T value) where T : notnull
    {
        var element = CakeJsonExtensions.DeserializeJson<JsonElement>(context.SerializeJson(value));
        return context.SerializeJson(Order(element), new JsonSerializerOptions { WriteIndented = false });
    }

    private static object Order(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element
            .EnumerateObject()
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .ToDictionary(property => property.Name, property => Order(property.Value), StringComparer.Ordinal),
        JsonValueKind.Array => element.EnumerateArray().Select(Order).ToArray(),
        JsonValueKind.Undefined or JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null => element,
        _ => throw new ArgumentOutOfRangeException(nameof(element)),
    };
}
