using System.Buffers;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace DKH.CustomerService.Domain.ValueObjects;

/// <summary>Version-one canonical bytes used by immutable journal payloads and snapshots.</summary>
public static class ExperienceCanonicalJson
{
    public const int MaximumPayloadBytes = 128 * 1024;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Serialize<T>(T value)
    {
        var element = JsonSerializer.SerializeToElement(value, SerializerOptions);
        return Canonicalize(element);
    }

    public static string Canonicalize(string json)
    {
        if (Encoding.UTF8.GetByteCount(json) > MaximumPayloadBytes)
        {
            throw new ArgumentException("Journal payload exceeds the UTF-8 byte limit.", nameof(json));
        }

        using var document = JsonDocument.Parse(json);
        return Canonicalize(document.RootElement);
    }

    public static string Hash(string canonicalJson)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson)));

    public static bool MatchesHash(string canonicalJson, string hash)
        => StringComparer.Ordinal.Equals(Hash(canonicalJson), hash);

    private static string Canonicalize(JsonElement element)
    {
        var bytes = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(bytes, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            Write(writer, element);
        }

        if (bytes.WrittenCount > MaximumPayloadBytes)
        {
            throw new ArgumentException("Journal payload exceeds the UTF-8 byte limit.");
        }

        return Encoding.UTF8.GetString(bytes.WrittenSpan);
    }

    private static void Write(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in element.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    if (!names.Add(property.Name))
                    {
                        throw new ArgumentException("Canonical journal JSON cannot contain duplicate property names.");
                    }

                    writer.WritePropertyName(property.Name);
                    Write(writer, property.Value);
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    Write(writer, item);
                }

                writer.WriteEndArray();
                break;
            case JsonValueKind.Number:
                if (!element.TryGetDecimal(out var number)
                    || NormalizeNumber(element.GetRawText()) != NormalizeNumber(number.ToString("G29", CultureInfo.InvariantCulture)))
                {
                    throw new ArgumentException("Canonical journal JSON requires exact finite decimal or integer values.");
                }

                writer.WriteRawValue(number.ToString("G29", CultureInfo.InvariantCulture));
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }

    private static string NormalizeNumber(string text)
    {
        // Decimal parsing can silently round an over-precise JSON number.
        // Compare its exact coefficient/exponent before accepting the parse.
        if (text.Length > 1024)
        {
            throw new ArgumentException("Canonical journal numeric token exceeds supported precision.");
        }

        var exponentIndex = text.IndexOfAny(['e', 'E']);
        var exponent = 0;
        if (exponentIndex >= 0
            && !int.TryParse(text.AsSpan(exponentIndex + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out exponent))
        {
            throw new ArgumentException("Canonical journal numeric exponent is invalid.");
        }

        var mantissa = exponentIndex < 0 ? text : text[..exponentIndex];
        var negative = mantissa[0] == '-';
        if (negative)
        {
            mantissa = mantissa[1..];
        }

        var point = mantissa.IndexOf('.');
        var fractionDigits = point < 0 ? 0 : mantissa.Length - point - 1;
        var digits = mantissa.Replace(".", string.Empty, StringComparison.Ordinal).TrimStart('0');
        if (digits.Length == 0)
        {
            return "0";
        }

        var coefficient = digits.TrimEnd('0');
        var normalizedExponent = (long)exponent - fractionDigits + digits.Length - coefficient.Length;
        return (negative ? "-" : string.Empty) + coefficient + "e" + normalizedExponent.ToString(CultureInfo.InvariantCulture);
    }
}
