namespace eInvWorld.Services
{
    public interface ITokenService
    {
        Task<string> GetAccessToken();
        Task<string> GetAccessTokenForTIN(string tin);
        Task<string> GetUserAssignedTINAsync();
    }
}
