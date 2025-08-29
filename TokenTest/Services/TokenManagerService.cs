using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using TokenManager.ClassLibrary.Interfaces;
using TokenManager.ClassLibrary.Models;

namespace TokenManager.ClassLibrary.Services
{
    public class TokenManagerService : ITokenManager
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<TokenManagerService> _logger;
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _tokenUrl;

        private string? _cachedToken;
        private DateTime? _tokenExpiresAt;
        private string _tokenType = "Bearer";

        private int _hourlyRequestCount = 0;
        private DateTime _lastHourReset = DateTime.Now;
        private const int MaxHourlyRequests = 5;

        private const double SafetyMarginRatio = 0.9;

        private readonly object _lock = new object();

        public TokenManagerService(
            HttpClient httpClient,
            ILogger<TokenManagerService> logger,
            string clientId,
            string clientSecret,
            string tokenUrl)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _clientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
            _clientSecret = clientSecret ?? throw new ArgumentNullException(nameof(clientSecret));
            _tokenUrl = tokenUrl ?? throw new ArgumentNullException(nameof(tokenUrl));
        }

        public bool IsTokenValid()
        {
            lock (_lock)
            {
                if (string.IsNullOrEmpty(_cachedToken) || !_tokenExpiresAt.HasValue)
                    return false;

                return DateTime.Now < _tokenExpiresAt.Value;
            }
        }

        public bool CanMakeTokenRequest()
        {
            lock (_lock)
            {
                CheckAndResetHourlyLimit();
                return _hourlyRequestCount < MaxHourlyRequests;
            }
        }

        public async Task<string> GetValidTokenAsync()
        {
            if (IsTokenValid())
            {
                _logger.LogDebug("Cache'den token kullanılıyor");
                return _cachedToken!;
            }

            return await FetchNewTokenAsync();
        }

        public async Task<string> GetAuthorizationHeaderAsync()
        {
            var token = await GetValidTokenAsync();
            return $"{_tokenType} {token}";
        }

        public TokenInfo GetTokenInfo()
        {
            lock (_lock)
            {
                var isValid = IsTokenValid();
                var remainingTime = isValid && _tokenExpiresAt.HasValue
                    ? (int)(_tokenExpiresAt.Value - DateTime.Now).TotalSeconds
                    : 0;

                return new TokenInfo
                {
                    HasToken = !string.IsNullOrEmpty(_cachedToken),
                    IsValid = isValid,
                    RemainingTimeSeconds = Math.Max(0, remainingTime),
                    HourlyRequestsUsed = _hourlyRequestCount,
                    HourlyRequestsRemaining = MaxHourlyRequests - _hourlyRequestCount,
                    ExpiresAt = _tokenExpiresAt
                };
            }
        }

        private void CheckAndResetHourlyLimit()
        {
            var now = DateTime.Now;
            if (now - _lastHourReset >= TimeSpan.FromHours(1))
            {
                _hourlyRequestCount = 0;
                _lastHourReset = now;
                _logger.LogInformation("Saatlik token istek sayacı sıfırlandı");
            }
        }

        private async Task<string> FetchNewTokenAsync()
        {
            if (!CanMakeTokenRequest())
            {
                var resetTime = _lastHourReset.Add(TimeSpan.FromHours(1));
                var message = $"Saatlik token limiti aşıldı. Sıfırlanma: {resetTime:HH:mm:ss}";
                _logger.LogWarning(message);
                throw new InvalidOperationException(message);
            }

            try
            {
                _logger.LogInformation("Yeni token alınıyor...");

                var credentials = Convert.ToBase64String(
                    Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}"));

                var request = new HttpRequestMessage(HttpMethod.Post, _tokenUrl);
                request.Headers.Add("Authorization", $"Basic {credentials}");
                request.Content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "client_credentials")
                });

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException(
                        $"Token isteği başarısız: {response.StatusCode}");
                }

                var content = await response.Content.ReadAsStringAsync();
                var tokenData = JsonSerializer.Deserialize<TokenResponse>(content);

                if (tokenData?.AccessToken == null)
                {
                    throw new InvalidOperationException("Geçersiz token yanıtı");
                }

                lock (_lock)
                {
                    _cachedToken = tokenData.AccessToken;
                    _tokenType = tokenData.TokenType ?? "Bearer";

                    var safeExpiresIn = TimeSpan.FromSeconds(tokenData.ExpiresIn * SafetyMarginRatio);
                    _tokenExpiresAt = DateTime.Now.Add(safeExpiresIn);
                    _hourlyRequestCount++;

                    _logger.LogInformation(
                        "Token alındı. Süre: {ExpiresIn}s, Kalan istek: {RemainingRequests}",
                        tokenData.ExpiresIn,
                        MaxHourlyRequests - _hourlyRequestCount);
                }

                return _cachedToken;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Token alma hatası");
                throw;
            }
        }
    }
}