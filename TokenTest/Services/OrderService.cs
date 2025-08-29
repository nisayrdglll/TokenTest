using Microsoft.Extensions.Logging;
using System.Text.Json;
using TokenManager.ClassLibrary.Interfaces;
using TokenManager.ClassLibrary.Models;

namespace TokenManager.ClassLibrary.Services
{
    public class OrderService : IOrderService, IDisposable
    {
        private readonly ITokenManager _tokenManager;
        private readonly HttpClient _httpClient;
        private readonly ILogger<OrderService> _logger;
        private readonly string _orderListUrl;

        private Timer? _periodicTimer;
        private bool _disposed = false;

        public event Action<List<OrderItem>>? OrderListUpdated;
        public event Action<Exception>? ErrorOccurred;

        public bool IsRunning => _periodicTimer != null;

        public OrderService(
            ITokenManager tokenManager,
            HttpClient httpClient,
            ILogger<OrderService> logger,
            string orderListUrl)
        {
            _tokenManager = tokenManager ?? throw new ArgumentNullException(nameof(tokenManager));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _orderListUrl = orderListUrl ?? throw new ArgumentNullException(nameof(orderListUrl));
        }

        public async Task<List<OrderItem>> GetOrderListAsync()
        {
            try
            {
                var authHeader = await _tokenManager.GetAuthorizationHeaderAsync();

                _logger.LogDebug("Sipariş listesi API çağrısı yapılıyor...");

                var request = new HttpRequestMessage(HttpMethod.Get, _orderListUrl);
                request.Headers.Add("Authorization", authHeader);

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException(
                        $"API hatası: {response.StatusCode} {response.ReasonPhrase}");
                }

                var content = await response.Content.ReadAsStringAsync();
                var orders = JsonSerializer.Deserialize<List<OrderItem>>(content) ?? new List<OrderItem>();

                _logger.LogInformation("Sipariş listesi alındı: {OrderCount} sipariş", orders.Count);
                return orders;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sipariş listesi alma hatası");
                throw;
            }
        }

        public void StartPeriodicFetch(int intervalMinutes = 5)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(OrderService));

            if (IsRunning)
            {
                _logger.LogWarning("Periyodik çekme zaten çalışıyor");
                return;
            }

            _logger.LogInformation("Periyodik sipariş çekme başlatıldı: {Interval} dakika", intervalMinutes);

            var interval = TimeSpan.FromMinutes(intervalMinutes);

            _periodicTimer = new Timer(async _ =>
            {
                try
                {
                    var orders = await GetOrderListAsync();
                    OrderListUpdated?.Invoke(orders);
                }
                catch (Exception ex)
                {
                    ErrorOccurred?.Invoke(ex);
                }
            }, null, TimeSpan.Zero, interval);
        }

        public void StopPeriodicFetch()
        {
            _periodicTimer?.Dispose();
            _periodicTimer = null;
            _logger.LogInformation("Periyodik sipariş çekme durduruldu");
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                StopPeriodicFetch();
                _disposed = true;
            }
        }
    }
}