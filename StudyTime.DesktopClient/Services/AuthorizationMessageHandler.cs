using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;
using StudyTime.Application.DTOs.Auth;

namespace StudyTime.DesktopClient.Services
{
    public class AuthorizationMessageHandler : DelegatingHandler
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly NavigationManager _navigationManager;

        public AuthorizationMessageHandler(
            IServiceProvider serviceProvider,
            NavigationManager navigationManager)
        {
            _serviceProvider = serviceProvider;
            _navigationManager = navigationManager;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var authStateProvider = _serviceProvider.GetService<CustomAuthenticationStateProvider>();
            if (authStateProvider == null)
                return await base.SendAsync(request, cancellationToken);

            var token = await authStateProvider.GetTokenAsync();

            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            // Tüm platformlar için eşsiz cihaz kimliğini X-Hardware-Id ile gönder
            var identityService = _serviceProvider.GetService<IDeviceIdentityService>();
            if (identityService != null)
            {
                var hwid = identityService.GetDeviceId();
                if (!string.IsNullOrEmpty(hwid))
                {
                    request.Headers.Add("X-Hardware-Id", hwid);
                }
            }

            var response = await base.SendAsync(request, cancellationToken);

            // Sunucu oturumu yokken (yerel-only / Bearer yok) 401 — yerel veriyi silme
            if (string.IsNullOrEmpty(token))
                return response;

            // 401 Unauthorized ise önce Refresh Token deniyoruz:
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                bool refreshed = await TryRefreshTokensAsync(cancellationToken);
                if (refreshed)
                {
                    var newToken = await authStateProvider.GetTokenAsync();
                    var clonedRequest = await CloneRequestAsync(request);
                    clonedRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newToken);
                    
                    response.Dispose();
                    response = await base.SendAsync(clonedRequest, cancellationToken);
                    
                    if (response.StatusCode != HttpStatusCode.Unauthorized)
                    {
                        return response;
                    }
                }
            }

            // Hala 401, veya 403/402 geldiyse oturumu kapat
            if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden || response.StatusCode == HttpStatusCode.PaymentRequired)
            {
                var content = await response.Content.ReadAsStringAsync();
                await authStateProvider.MarkUserAsLoggedOut();
                
                bool isMismatch = content.Contains("SESSION_MISMATCH");
                bool isPremiumExpired = content.Contains("PREMIUM_EXPIRED");

                if (isMismatch)
                {
                    await Microsoft.Maui.Storage.SecureStorage.Default.SetAsync("LoginError", "session_mismatch");
                }
                else if (isPremiumExpired)
                {
                    await Microsoft.Maui.Storage.SecureStorage.Default.SetAsync("LoginError", "premium_expired");
                }

                try
                {
                    _ = Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        try
                        {
                            if (isMismatch)
                            {
                                _navigationManager.NavigateTo("/login?error=session_mismatch", forceLoad: false);
                            }
                            else if (isPremiumExpired)
                            {
                                _navigationManager.NavigateTo("/login?error=premium_expired", forceLoad: false);
                            }
                            else
                            {
                                _navigationManager.NavigateTo("/login", forceLoad: false);
                            }
                        }
                        catch (InvalidOperationException)
                        {
                            // Blazor WebView henüz başlatılmadıysa NavigateTo patlar, yoksayıyoruz.
                            // AuthStateProvider tetiklendiği için UI açıldığında zaten login'e düşecek.
                        }
                        catch { }
                    });
                }
                catch { }
            }

            return response;
        }

        private async Task<bool> TryRefreshTokensAsync(CancellationToken cancellationToken)
        {
            var authStateProvider = _serviceProvider.GetService<CustomAuthenticationStateProvider>();
            if (authStateProvider == null)
                return false;

            var accessToken = await authStateProvider.GetTokenAsync();
            var refreshToken = await authStateProvider.GetRefreshTokenAsync();

            if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken))
                return false;

            var request = new TokenRequestDto
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken
            };

            // NoAuth client: refresh çağrısında AuthorizationMessageHandler devreye girmez.
            var httpClientFactory = _serviceProvider.GetRequiredService<IHttpClientFactory>();
            var refreshClient = httpClientFactory.CreateClient("StudyTimeApiNoAuth");
            var response = await refreshClient.PostAsJsonAsync("api/auth/refresh", request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return false;

            var result = await response.Content.ReadFromJsonAsync<AuthResponseDto>(cancellationToken: cancellationToken);
            if (result == null || string.IsNullOrEmpty(result.Token))
                return false;

            await authStateProvider.MarkUserAsAuthenticated(result.Token, result.RefreshToken);
            return true;
        }
        private async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage req)
        {
            var clone = new HttpRequestMessage(req.Method, req.RequestUri)
            {
                Version = req.Version
            };
            
            if (req.Content != null)
            {
                var ms = new MemoryStream();
                await req.Content.CopyToAsync(ms);
                ms.Position = 0;
                clone.Content = new StreamContent(ms);
                foreach (var h in req.Content.Headers)
                    clone.Content.Headers.Add(h.Key, h.Value);
            }
            
            foreach (var h in req.Headers)
                clone.Headers.TryAddWithoutValidation(h.Key, h.Value);
                
            return clone;
        }
    }
}
