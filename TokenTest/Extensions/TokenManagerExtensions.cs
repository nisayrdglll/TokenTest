using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TokenManager.ClassLibrary.Interfaces;
using TokenManager.ClassLibrary.Services;

namespace TokenManager.ClassLibrary.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddTokenManager(
            this IServiceCollection services,
            string clientId,
            string clientSecret,
            string tokenUrl,
            string orderListUrl)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (string.IsNullOrEmpty(clientId)) throw new ArgumentException("ClientId gerekli", nameof(clientId));
            if (string.IsNullOrEmpty(clientSecret)) throw new ArgumentException("ClientSecret gerekli", nameof(clientSecret));
            if (string.IsNullOrEmpty(tokenUrl)) throw new ArgumentException("TokenUrl gerekli", nameof(tokenUrl));
            if (string.IsNullOrEmpty(orderListUrl)) throw new ArgumentException("OrderListUrl gerekli", nameof(orderListUrl));

            services.AddHttpClient();

            services.AddSingleton<ITokenManager>(provider =>
            {
                var httpClient = provider.GetRequiredService<HttpClient>();
                var logger = provider.GetRequiredService<ILogger<TokenManagerService>>();
                return new TokenManagerService(httpClient, logger, clientId, clientSecret, tokenUrl);
            });

            services.AddScoped<IOrderService>(provider =>
            {
                var tokenManager = provider.GetRequiredService<ITokenManager>();
                var httpClient = provider.GetRequiredService<HttpClient>();
                var logger = provider.GetRequiredService<ILogger<OrderService>>();
                return new OrderService(tokenManager, httpClient, logger, orderListUrl);
            });

            return services;
        }

        public static IServiceCollection AddTokenManager(
            this IServiceCollection services,
            Action<TokenManagerOptions> configureOptions)
        {
            var options = new TokenManagerOptions();
            configureOptions(options);

            return services.AddTokenManager(
                options.ClientId,
                options.ClientSecret,
                options.TokenUrl,
                options.OrderListUrl);
        }
    }

    public class TokenManagerOptions
    {
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string TokenUrl { get; set; } = string.Empty;
        public string OrderListUrl { get; set; } = string.Empty;
    }
}