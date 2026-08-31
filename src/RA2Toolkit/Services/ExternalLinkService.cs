using System.Diagnostics;

internal static class ExternalLinkService
{
    private static readonly HashSet<string> AllowedHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "github.com"
    };

    internal static void Open(Uri uri)
    {
        if (uri.Scheme != Uri.UriSchemeHttps || !AllowedHosts.Contains(uri.Host))
            throw new InvalidOperationException("已阻止打开不受信任的外部地址。");
        Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    }
}
