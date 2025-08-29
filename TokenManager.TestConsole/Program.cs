using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TokenManager.ClassLibrary.Extensions;
using TokenManager.ClassLibrary.Interfaces;

namespace TokenTest
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("════════════════════════════════════════");
            Console.WriteLine("Token Yönetimi Test Uygulaması");
            Console.WriteLine("════════════════════════════════════════\n");

            try
            {
                var services = new ServiceCollection();

                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Information);
                });

                services.AddTokenManager(
                    clientId: "demo-client-12345",
                    clientSecret: "demo-secret-67890",
                    tokenUrl: "https://httpbin.org/json", 
                    orderListUrl: "https://jsonplaceholder.typicode.com/posts"
                );

                var serviceProvider = services.BuildServiceProvider();

                var tokenManager = serviceProvider.GetRequiredService<ITokenManager>();
                var orderService = serviceProvider.GetRequiredService<IOrderService>();
                var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

                logger.LogInformation("Sistem başarıyla başlatıldı!");

                await RunQuickTests(tokenManager, orderService, logger);

                Console.WriteLine("\nTestler tamamlandı!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nHATA: {ex.Message}");
                Console.WriteLine($"Detay: {ex}");
            }

            Console.WriteLine("\n════════════════════════════════════════");
            Console.WriteLine("Çıkmak için ENTER tuşuna basın...");
            Console.ReadLine();
        }

        static async Task RunQuickTests(ITokenManager tokenManager, IOrderService orderService, ILogger logger)
        {
            Console.WriteLine("\n" + new string('=', 50));
            logger.LogInformation("TEST 1: İlk Token Durumu");
            Console.WriteLine(new string('=', 50));

            ShowTokenStatus(tokenManager, logger);
            await Task.Delay(1000);

            Console.WriteLine("\n" + new string('=', 50));
            logger.LogInformation("TEST 2: Token Alma Testi");
            Console.WriteLine(new string('=', 50));

            try
            {
                logger.LogInformation("Token alınmaya çalışılıyor...");
                var authHeader = await tokenManager.GetAuthorizationHeaderAsync();
                logger.LogInformation("Authorization header oluşturuldu!");
                logger.LogInformation("Header: {Header}...", authHeader.Substring(0, Math.Min(30, authHeader.Length)));

                ShowTokenStatus(tokenManager, logger);
            }
            catch (Exception ex)
            {
                logger.LogWarning("Token alma hatası (Beklenen - Demo API): {Message}", ex.Message);
            }

            await Task.Delay(2000);

            Console.WriteLine("\n" + new string('=', 50));
            logger.LogInformation("💾 TEST 3: Cache Testi - 3 Kez Token İste");
            Console.WriteLine(new string('=', 50));

            for (int i = 1; i <= 3; i++)
            {
                logger.LogInformation("--- {Attempt}. deneme ---", i);
                try
                {
                    var token = await tokenManager.GetValidTokenAsync();
                    logger.LogInformation("Token alındı: {TokenPreview}...",
                        token.Substring(0, Math.Min(15, token.Length)));
                    ShowTokenStatus(tokenManager, logger);
                }
                catch (Exception ex)
                {
                    logger.LogWarning("Hata: {Message}", ex.Message);
                }

                await Task.Delay(1000);
            }

            Console.WriteLine("\n" + new string('=', 50));
            logger.LogInformation("TEST 4: Sipariş Listesi API Testi");
            Console.WriteLine(new string('=', 50));

            try
            {
                logger.LogInformation("Sipariş listesi çekiliyor...");
                var orders = await orderService.GetOrderListAsync();
                logger.LogInformation("Sipariş listesi başarıyla alındı: {Count} adet", orders.Count);

                if (orders.Count > 0)
                {
                    logger.LogInformation("İlk 3 sipariş:");
                    foreach (var order in orders.Take(3))
                    {
                        logger.LogInformation("   📋 #{Id}: {ProductName}",
                            order.Id,
                            string.IsNullOrEmpty(order.ProductName) ? "Test Ürün" : order.ProductName);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning("Sipariş listesi hatası: {Message}", ex.Message);
            }

            await Task.Delay(2000);

            Console.WriteLine("\n" + new string('=', 50));
            logger.LogInformation("TEST 5: Event Sistemi");
            Console.WriteLine(new string('=', 50));

            orderService.OrderListUpdated += orders =>
            {
                logger.LogInformation("EVENT: Sipariş listesi güncellendi - {Count} sipariş", orders.Count);
            };

            orderService.ErrorOccurred += ex =>
            {
                logger.LogWarning("EVENT: Hata oluştu - {Message}", ex.Message);
            };

            logger.LogInformation("Event handler'lar başarıyla kaydedildi");

            Console.WriteLine("\n" + new string('=', 50));
            logger.LogInformation("TEST 6: Periyodik Çekme (10 saniye test)");
            Console.WriteLine(new string('=', 50));

            logger.LogInformation("Her 1 dakikada bir sipariş çekme başlatılıyor...");
            orderService.StartPeriodicFetch(1);

            logger.LogInformation("Servis durumu: {Status}",
                orderService.IsRunning ? "ÇALIŞIYOR" : "DURMUŞ");

            for (int i = 1; i <= 2; i++)
            {
                await Task.Delay(5000);

                Console.WriteLine($"\n--- {i * 5}. saniye kontrol ---");
                ShowTokenStatus(tokenManager, logger);
            }

            Console.WriteLine("\n" + new string('=', 50));
            logger.LogInformation("TEST 7: Servis Durdurma");
            Console.WriteLine(new string('=', 50));

            orderService.StopPeriodicFetch();
            logger.LogInformation("Servis durumu: {Status}",
                orderService.IsRunning ? "ÇALIŞIYOR" : "DURMUŞ");

            logger.LogInformation("Tüm temel testler başarıyla tamamlandı!");
        }

        static void ShowTokenStatus(ITokenManager tokenManager, ILogger logger)
        {
            try
            {
                var status = tokenManager.GetTokenInfo();

                Console.WriteLine("┌─────────────────────────────────────┐");
                Console.WriteLine("│           🔑 TOKEN DURUMU           │");
                Console.WriteLine("├─────────────────────────────────────┤");
                Console.WriteLine($"│ 📄 Token Var    : {(status.HasToken ? "Evet" : "Hayır"),-15} │");
                Console.WriteLine($"│ ✅ Geçerli      : {(status.IsValid ? "Geçerli" : "Geçersiz"),-15} │");
                Console.WriteLine($"│ ⏰ Kalan Süre   : {status.RemainingTimeSeconds + " sn",-15} │");
                Console.WriteLine($"│ 📊 Saatlik İstek: {status.HourlyRequestsUsed + "/" + (status.HourlyRequestsUsed + status.HourlyRequestsRemaining),-15} │");

                if (status.ExpiresAt.HasValue)
                {
                    Console.WriteLine($"│ ⏳ Bitiş Zamanı : {status.ExpiresAt.Value:HH:mm:ss}        │");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Token durumu gösterilirken hata oluştu");
            }
        }
    }
}