using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Threading.Tasks;
using System;

namespace TokenManager.TestConsole
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== Token Yönetimi Test Uygulaması ===\n");

            try
            {
                var services = new ServiceCollection();

                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Information);
                });

                services.AddHttpClient();

                services.AddSingleton<TokenTest.Interfaces.ITokenManager>(provider =>
                {
                    var httpClient = provider.GetRequiredService<HttpClient>();
                    var logger = provider.GetRequiredService<ILogger<TokenTest.Services.TokenManagerService>>();
                    return new TokenTest.Services.TokenManagerService(
                        httpClient,
                        logger,
                        "demo-client-id",
                        "demo-client-secret",
                        "https://httpbin.org/json"
                    );
                });

                services.AddScoped<TokenTest.Interfaces.IOrderService>(provider =>
                {
                    var tokenManager = provider.GetRequiredService<TokenTest.Interfaces.ITokenManager>();
                    var httpClient = provider.GetRequiredService<HttpClient>();
                    var logger = provider.GetRequiredService<ILogger<TokenTest.Services.OrderService>>();
                    return new TokenTest.Services.OrderService(
                        tokenManager,
                        httpClient,
                        logger,
                        "https://jsonplaceholder.typicode.com/posts"
                    );
                });

                var serviceProvider = services.BuildServiceProvider();

                var tokenManager = serviceProvider.GetRequiredService<TokenTest.Interfaces.ITokenManager>();
                var orderService = serviceProvider.GetRequiredService<TokenTest.Interfaces.IOrderService>();
                var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

                logger.LogInformation("Sistem başlatılıyor...");

                await RunSimpleTests(tokenManager, orderService, logger);

                logger.LogInformation("✅ Sistem başarıyla tamamlandı!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ HATA: {ex.Message}");
                Console.WriteLine($"Detay: {ex}");
            }

            Console.WriteLine("\n🔚 Çıkmak için bir tuşa basın...");
            Console.ReadKey();
        }

        static async Task RunSimpleTests(
            TokenTest.Interfaces.ITokenManager tokenManager,
            TokenTest.Interfaces.IOrderService orderService,
            ILogger logger)
        {
            logger.LogInformation("\n=== TEST 1: İlk Token Durumu ===");
            ShowTokenStatus(tokenManager, logger);

            logger.LogInformation("\n=== TEST 2: Token Alma Testi ===");
            try
            {
                logger.LogInformation("Token almaya çalışıyorum...");
                var authHeader = await tokenManager.GetAuthorizationHeaderAsync();
                logger.LogInformation("✅ Authorization header oluşturuldu!");
                logger.LogInformation("Header: {Header}", authHeader.Substring(0, Math.Min(30, authHeader.Length)) + "...");

                ShowTokenStatus(tokenManager, logger);
            }
            catch (Exception ex)
            {
                logger.LogWarning("⚠️ Token alma hatası (Normal - demo API): {Message}", ex.Message);
            }

            logger.LogInformation("\n=== TEST 3: Token Limit Testi ===");
            logger.LogInformation("Aynı token'ı tekrar isteyince cache'den gelecek mi test ediyoruz...");

            for (int i = 1; i <= 3; i++)
            {
                try
                {
                    logger.LogInformation("--- {AttemptNumber}. deneme ---", i);
                    var token = await tokenManager.GetValidTokenAsync();
                    logger.LogInformation("Token alındı (ilk 20 karakter): {Token}...",
                        token.Substring(0, Math.Min(20, token.Length)));
                    ShowTokenStatus(tokenManager, logger);

                    await Task.Delay(2000);
                }
                catch (Exception ex)
                {
                    logger.LogWarning("Hata: {Message}", ex.Message);
                }
            }

            logger.LogInformation("\n=== TEST 4: Event Sistemi Testi ===");

            orderService.OrderListUpdated += orders =>
            {
                logger.LogInformation("📝 EVENT: Sipariş listesi güncellendi - {Count} sipariş", orders?.Count ?? 0);
            };

            orderService.ErrorOccurred += ex =>
            {
                logger.LogWarning("⚠️ EVENT: Hata oluştu - {Message}", ex?.Message ?? "Bilinmeyen hata");
            };

            logger.LogInformation("Event handler'lar kaydedildi ✅");

            logger.LogInformation("\n=== TEST 5: Sipariş Listesi Testi ===");
            try
            {
                logger.LogInformation("Sipariş listesi alınıyor...");
                var orders = await orderService.GetOrderListAsync();
                logger.LogInformation("✅ Sipariş listesi başarıyla alındı: {Count} adet", orders?.Count ?? 0);

                if (orders != null && orders.Count > 0)
                {
                    logger.LogInformation("İlk 3 sipariş:");
                    foreach (var order in orders.Take(3))
                    {
                        logger.LogInformation("   📦 Sipariş #{Id}: {ProductName}",
                            order.Id, order.ProductName ?? "İsimsiz ürün");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning("⚠️ Sipariş listesi hatası: {Message}", ex.Message);
            }

            logger.LogInformation("\n🎉 Tüm basit testler tamamlandı!");
        }

        static void ShowTokenStatus(TokenTest.Interfaces.ITokenManager tokenManager, ILogger logger)
        {
            try
            {
                var status = tokenManager.GetTokenInfo();

                logger.LogInformation("🔑 TOKEN DURUMU:");
                logger.LogInformation("   📄 Token Var: {HasToken}", status.HasToken ? "✅ Evet" : "❌ Hayır");
                logger.LogInformation("   ✅ Geçerli: {IsValid}", status.IsValid ? "✅ Evet" : "❌ Hayır");
                logger.LogInformation("   ⏰ Kalan Süre: {RemainingTime} saniye", status.RemainingTimeSeconds);
                logger.LogInformation("   📊 Saatlik İstek: {Used}/{Total}",
                    status.HourlyRequestsUsed,
                    status.HourlyRequestsUsed + status.HourlyRequestsRemaining);

                if (status.ExpiresAt.HasValue)
                {
                    logger.LogInformation("   ⏳ Bitiş Zamanı: {ExpiresAt:HH:mm:ss}", status.ExpiresAt.Value);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Token durumu gösterilirken hata oluştu");
            }
        }
    }
}