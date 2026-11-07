using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace ApexToolbox.Services;

public sealed class SharePayload
{
    public int Version { get; set; } = 2;
    public string Name { get; set; } = string.Empty;
    public string Created { get; set; } = string.Empty;

    /// <summary>完整文件原文（保留注释、顺序、空行与缩进）。</summary>
    public string SettingsText { get; set; } = string.Empty;
    public string VideoText { get; set; } = string.Empty;
    public string SettingsEncoding { get; set; } = "utf-8";
    public string VideoEncoding { get; set; } = "utf-8";

    /// <summary>旧版分享码的键值列表，保留用于兼容。</summary>
    public Dictionary<string, string> Settings { get; set; } = new();
    public Dictionary<string, string> Video { get; set; } = new();
}

/// <summary>把画面配置压缩成可直接粘贴分享的短码。</summary>
public static class ShareCodec
{
    private const string Prefix = "APX1-";

    public static string Encode(SharePayload payload)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(payload);
        using var output = new MemoryStream();
        using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            deflate.Write(json, 0, json.Length);
        }

        var base64 = Convert.ToBase64String(output.ToArray());
        var urlSafe = base64.TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return Prefix + urlSafe;
    }

    public static SharePayload? Decode(string code)
    {
        var text = code.Trim();
        if (!text.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)) return null;

        var body = text[Prefix.Length..].Trim().Replace('-', '+').Replace('_', '/');
        var padding = body.Length % 4;
        if (padding != 0) body += new string('=', 4 - padding);

        try
        {
            var compressed = Convert.FromBase64String(body);
            using var input = new MemoryStream(compressed);
            using var deflate = new DeflateStream(input, CompressionMode.Decompress);
            using var reader = new MemoryStream();
            deflate.CopyTo(reader);
            return JsonSerializer.Deserialize<SharePayload>(reader.ToArray());
        }
        catch
        {
            return null;
        }
    }
}
