using System.Text.Json.Serialization;

namespace TokenManager.ClassLibrary.Models
{
    public class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = "Bearer";

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }

    public class TokenInfo
    {
        public bool HasToken { get; set; }
        public bool IsValid { get; set; }
        public int RemainingTimeSeconds { get; set; }
        public int HourlyRequestsUsed { get; set; }
        public int HourlyRequestsRemaining { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }

    public class OrderItem
    {
        public int Id { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public DateTime OrderDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
    }

    public class ApiError
    {
        public string Message { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}