using Microsoft.Extensions.DependencyInjection;

namespace TishtryaCMS.SharedKernel.Turnstile;

public static class TurnstileServiceCollectionExtensions
{
    public static IServiceCollection AddTurnstileValidator(this IServiceCollection services)
    {
        services.AddHttpClient(TurnstileValidator.HttpClientName, client =>
        {
            client.BaseAddress = new Uri("https://challenges.cloudflare.com/");
        });
        services.AddSingleton<ITurnstileValidator, TurnstileValidator>();
        return services;
    }
}
