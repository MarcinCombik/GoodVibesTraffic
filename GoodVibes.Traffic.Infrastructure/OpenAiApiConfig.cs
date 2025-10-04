using Microsoft.Extensions.DependencyInjection;

namespace GoodVibes.Traffic.Infrastructure;

public static class OpenAiApiConfig
{
    public const string HTTP_CLIENT_NAME = "OpenAI";
    public static IServiceCollection AddOpenAiClient(this IServiceCollection services, string apiKey, string organizationId)
    {
        services.AddHttpClient(HTTP_CLIENT_NAME, c =>
        {
            c.BaseAddress = new Uri("https://api.openai.com/");
            c.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
            c.DefaultRequestHeaders.Add("OpenAI-Organization", organizationId);
        });

        return services;
    }
}