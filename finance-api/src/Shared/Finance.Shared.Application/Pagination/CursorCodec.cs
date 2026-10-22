using System.Buffers.Text;
using System.Text.Json;

namespace Finance.Shared.Application.Pagination;

public static class CursorCodec
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static string Encode<TKey>(TKey key)
        where TKey : class
    {
        return Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(key, SerializerOptions));
    }

    public static bool TryDecode<TKey>(string? cursor, out TKey? key)
        where TKey : class
    {
        key = null;

        if (string.IsNullOrWhiteSpace(cursor))
        {
            return false;
        }

        try
        {
            key = JsonSerializer.Deserialize<TKey>(Base64Url.DecodeFromChars(cursor), SerializerOptions);
            return key is not null;
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            return false;
        }
    }
}
