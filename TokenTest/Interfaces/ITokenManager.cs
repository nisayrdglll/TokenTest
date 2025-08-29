using TokenManager.ClassLibrary.Models;

namespace TokenManager.ClassLibrary.Interfaces
{
    public interface ITokenManager
    {
        Task<string> GetValidTokenAsync();

        Task<string> GetAuthorizationHeaderAsync();

        TokenInfo GetTokenInfo();

        bool IsTokenValid();

        bool CanMakeTokenRequest();
    }
}