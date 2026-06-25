using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Maui.Devices;
using StudyTime.Application.DTOs.Auth;

namespace StudyTime.DesktopClient.Services
{
    public class AuthService
    {
        private readonly HttpClient _authHttpClient;
        private readonly HttpClient _noAuthHttpClient;
        private readonly CustomAuthenticationStateProvider _authStateProvider;
        private readonly IServiceProvider _serviceProvider;

        public AuthService(IHttpClientFactory httpClientFactory, CustomAuthenticationStateProvider authStateProvider, IServiceProvider serviceProvider)
        {
            _authHttpClient = httpClientFactory.CreateClient("StudyTimeApi");
            _noAuthHttpClient = httpClientFactory.CreateClient("StudyTimeApiNoAuth");
            _authStateProvider = authStateProvider;
            _serviceProvider = serviceProvider;
        }

        public async Task<bool> LoginAsync(string email, string password)
        {
            string? hwid = null;
            var identityService = _serviceProvider.GetService<IDeviceIdentityService>();
            if (identityService != null)
            {
                hwid = identityService.GetDeviceId();
            }

            string clientType = DeviceInfo.Idiom == DeviceIdiom.Desktop ? "Desktop" : "Mobile";

            var request = new LoginRequestDto
            {
                Email = email,
                Password = password,
                ClientType = clientType,
                Hwid = hwid
            };

            var response = await _noAuthHttpClient.PostAsJsonAsync("api/auth/login", request);

            if (response.IsSuccessStatusCode)
            {
                try
                {
                    var result = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
                    if (result != null && !string.IsNullOrEmpty(result.Token))
                    {
                        await _authStateProvider.MarkUserAsAuthenticated(result.Token, result.RefreshToken);
                        return true;
                    }
                }
                catch (JsonException ex)
                {
                    var rawContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Sunucudan geçersiz bir yanıt alındı. Yanıt: '{rawContent}'. Detay: {ex.Message}");
                }
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden ||
                     response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                     response.StatusCode == System.Net.HttpStatusCode.PaymentRequired)
            {
                var errorMsg = await response.Content.ReadAsStringAsync();
                if (errorMsg.Contains("DESKTOP_PREMIUM_REQUIRED"))
                {
                    throw new UnauthorizedAccessException("DESKTOP_PREMIUM_REQUIRED");
                }
                if (errorMsg.Contains("PREMIUM_REQUIRED"))
                {
                    throw new UnauthorizedAccessException("PREMIUM_REQUIRED");
                }
                if (errorMsg.Contains("DEVICE_LIMIT_REACHED"))
                {
                    throw new UnauthorizedAccessException("DEVICE_LIMIT_REACHED");
                }
            }

            return false;
        }

        public async Task<bool> RegisterAsync(string fullName, string email, string password)
        {
            string? hwid = null;
            var identityService = _serviceProvider.GetService<IDeviceIdentityService>();
            if (identityService != null)
            {
                hwid = identityService.GetDeviceId();
            }

            string clientType = DeviceInfo.Idiom == DeviceIdiom.Desktop ? "Desktop" : "Mobile";

            var request = new RegisterRequestDto
            {
                FullName = fullName,
                Email = email,
                Password = password,
                ClientType = clientType,
                Hwid = hwid
            };

            var response = await _noAuthHttpClient.PostAsJsonAsync("api/auth/register", request);

            if (response.IsSuccessStatusCode)
            {
                // Kullanıcı başarıyla kayıt oldu. Otomatik giriş yapma, sadece true dön.
                return true;
            }

            var errorMsg = await response.Content.ReadAsStringAsync();
            throw new Exception($"Kayıt başarısız (Durum: {(int)response.StatusCode}): {errorMsg}");
        }

        public async Task LogoutAsync()
        {
            try
            {
                // Backend'deki session'ı sonlandır - takılmaması için 3 sn timeout koy
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                await _authHttpClient.PostAsync("api/auth/logout", null, cts.Token);
            }
            catch
            {
                // Ağ hatası olsa bile local temizliği yapmak için hatayı yut
            }

            await _authStateProvider.MarkUserAsLoggedOut();
        }

        public async Task<bool> RefreshTokensAsync()
        {
            var accessToken = await _authStateProvider.GetTokenAsync();
            var refreshToken = await _authStateProvider.GetRefreshTokenAsync();

            if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken))
                return false;

            var request = new TokenRequestDto
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken
            };

            var response = await _noAuthHttpClient.PostAsJsonAsync("api/auth/refresh", request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
                if (result != null && !string.IsNullOrEmpty(result.Token))
                {
                    await _authStateProvider.MarkUserAsAuthenticated(result.Token, result.RefreshToken);
                    return true;
                }
            }

            return false;
        }

        public async Task<bool> UpdateProfileAsync(UpdateProfileRequestDto request)
        {
            var response = await _authHttpClient.PutAsJsonAsync("api/auth/profile", request);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> ChangePasswordAsync(ChangePasswordRequestDto request)
        {
            var response = await _authHttpClient.PutAsJsonAsync("api/auth/password", request);
            return response.IsSuccessStatusCode;
        }
    }
}
