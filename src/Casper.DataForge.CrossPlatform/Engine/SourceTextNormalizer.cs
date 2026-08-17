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
        if (result.Length == 0)
            return string.Empty;

        if (result.StartsWith("//", StringComparison.Ordinal))
            return "https:" + result;

        if (Uri.TryCreate(result, UriKind.Absolute, out Uri? absolute) &&
            (absolute.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
             absolute.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            var builder = new UriBuilder(absolute)
            {
                Fragment = string.Empty
            };

            if (builder.Path.Length == 0)
                builder.Path = "/";

            if ((builder.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) && builder.Port == 80) ||
                (builder.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) && builder.Port == 443))
            {
                builder.Port = -1;
            }

            return builder.Uri.AbsoluteUri;
        }

        return result;
    }
}
