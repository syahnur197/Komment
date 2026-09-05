namespace Dashboard;

// The one thing the Dashboard server still needs to know about the API: its
// public address, so the browser can be told where to send its own requests.
//
// Aspire injects services__backend__https__0 / __http__0 for the *server* to
// dial. In development those are the same URLs the browser uses, so they work as
// they are. In Docker they are not — the compose network name "backend" means
// nothing outside it — so production sets BACKEND_PUBLIC_URL explicitly.
public sealed class ApiBaseUrl(IConfiguration cfg)
{
    public string Value { get; } =
        First(cfg["BACKEND_PUBLIC_URL"])
        ?? First(cfg["Services:backend:https:0"])
        ?? First(cfg["Services:backend:http:0"])
        ?? throw new InvalidOperationException(
            "No backend URL. Set BACKEND_PUBLIC_URL to the address the browser should call.");

    // Service discovery permits a comma-separated list; the browser needs one.
    private static string? First(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Split(',')[0].Trim().TrimEnd('/');
}
