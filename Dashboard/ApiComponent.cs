using System.Net.Http.Json;
using System.Text.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace Dashboard;

// Every page that talks to the API needs the same three things: the configured
// client, a message when the API is unreachable, and the field error out of a
// FastEndpoints failure. Kept here so pages don't each grow their own copy.
public abstract class ApiComponent : ComponentBase
{
    [Inject] protected IHttpClientFactory HttpClientFactory { get; set; } = default!;

    protected HttpClient Api => HttpClientFactory.CreateClient(BackendSessionHandler.ClientName);

    protected string? error;

    [CascadingParameter] private Task<AuthenticationState>? AuthState { get; set; }

    // Who is signed in. Pages need it to tell "your comment" from "someone
    // else's", which is the difference between an Edit link and a 403.
    protected async Task<Guid?> CurrentUserIdAsync()
    {
        if (AuthState is null) return null;
        var user = (await AuthState).User;
        return Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    }

    // The API being down is not the user's mistake, and a stack trace is not an answer.
    protected async Task GuardAsync(Func<Task> body)
    {
        try
        {
            await body();
        }
        catch (HttpRequestException)
        {
            error = "Cannot reach the Komment API. Please try again in a moment.";
        }
    }

    // FastEndpoints' Send.ErrorsAsync shape: { errors: { field: [message] } }.
    protected static async Task<string?> FirstErrorAsync(HttpResponseMessage response)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync<ApiErrors>();
            return body?.Errors?.SelectMany(e => e.Value).FirstOrDefault();
        }
        catch (Exception e) when (e is JsonException or NotSupportedException)
        {
            return null;
        }
    }

    private sealed record ApiErrors(Dictionary<string, string[]>? Errors);
}
