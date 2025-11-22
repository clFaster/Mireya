namespace Mireya.ApiClient.Services;

public interface IAccessTokenProvider
{
    string? GetAccessToken();
    void SetAccessToken(string? token);
}
