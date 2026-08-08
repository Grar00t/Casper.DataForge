using System;
using System.Net;

namespace Casper.DataForge.CrossPlatform.Engine;

public static class SourceTextNormalizer
{
    public static string DecodeHtml(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        string current = value;

        for (int pass = 0; pass < 4; pass++)
        {
            string decoded = WebUtility.HtmlDecode(current);

            if (string.Equals(decoded, current, StringComparison.Ordinal))
                break;

            current = decoded;
        }

        return current;
    }

    public static string NormalizeUrl(string? value)
    {
        string result = DecodeHtml(value).Trim();

        if (result.StartsWith("//", StringComparison.Ordinal))
            result = "https:" + result;

        return result;
    }
}
