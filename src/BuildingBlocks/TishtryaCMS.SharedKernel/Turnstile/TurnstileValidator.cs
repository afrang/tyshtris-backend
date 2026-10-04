using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;

namespace TishtryaCMS.SharedKernel.Turnstile;

public sealed class TurnstileValidator(IConfiguration config, IHttpClientFactory httpClientFactory)
    : ITurnstileValidator
{
    public const string HttpClientName = "CloudflareTurnstile";

    public async Task<string?> ValidateAsync(
        string? captchaToken,
        CancellationToken cancellationToken = default)
    {
        var secretKey = config["Turnstile:SecretKey"];
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            // Not configured → skip (local/dev without keys).
            return null;
        }

        if (string.IsNullOrWhiteSpace(captchaToken))
        {
            return "Captcha challenge is required.";
        }

        try
        {
            var client = httpClientFactory.CreateClient(HttpClientName);
            using var payload = new FormUrlEncodedContent(new Dictionary<string, string?>
            {
                ["secret"] = secretKey,
                ["response"] = captchaToken
            });

            using var response = await client.PostAsync(
                "turnstile/v0/siteverify",
                payload,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<TurnstileResponse>(cancellationToken);
            if (result is null || !result.Success)
            {
                return "Captcha verification failed. Please try again.";
            }

            return null;
        }
        catch
        {
            return "Captcha verification unavailable. Please try again shortly.";
        }
    }

    private sealed class TurnstileResponse
    {
        public bool Success { get; set; }
    }
}
