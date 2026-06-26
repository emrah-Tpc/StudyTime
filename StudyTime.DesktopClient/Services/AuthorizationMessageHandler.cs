using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;
using StudyTime.Application.DTOs.Auth;

namespace StudyTime.DesktopClient.Services
{
    public class AuthorizationMessageHandler : DelegatingHandler
    {
        private static readonly SemaphoreSlim RefreshLock = new(1, 1);
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
            // POST/PUT gövdesini önceden tampona al: 401 sonrası refresh + retry'da istek klonlanıp
            // yeniden gönderildiğinde gövdenin tekrar okunabilmesi gerekir. Aksi halde ilk gönderimde
            // tüketilen gövde retry'da boş gider ve istek başarısız olur (özellikle ders ekleme gibi POST'larda
            // "bazen unauthorize" görülmesinin kök nedeni budur).
            if (request.Content != null)
                await request.Content.LoadIntoBufferAsync();

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
                bool refreshed = await TryRefreshTokensAsync(token, cancellationToken);
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

            // Sadece 401 (refresh de basarisiz) durumunda oturumu kapat.
            // 403/402 yetki/abonelik problemlerinde local veriyi silmek, kullaniciya "her sey silindi" gibi gorunur.
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                var content = await response.Content.ReadAsStringAsync();
                await authStateProvider.MarkUserAsLoggedOut(wipeLocalData: false);
                
                // Kullanıcı kendi çıkış yaptıysa, çıkış sonrası arka plan isteklerinden gelen
                // SESSION_MISMATCH'i "başka cihazda açıldı" olarak GÖSTERME (yanlış mesaj).
                bool isMismatch = content.Contains("SESSION_MISMATCH")
                                  && !CustomAuthenticationStateProvider.UserInitiatedLogout;
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

        private async Task<bool> TryRefreshTokensAsync(string failedAccessToken, CancellationToken cancellationToken)
        {
            var authStateProvider = _serviceProvider.GetService<CustomAuthenticationStateProvider>();
            if (authStateProvider == null)
                return false;

            var accessToken = await authStateProvider.GetTokenAsync();
            var refreshToken = await authStateProvider.GetRefreshTokenAsync();

            if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken))
                return false;

            // Baska bir istek bu arada token yenilediyse yeniden refresh denemeyelim.
            if (!string.Equals(accessToken, failedAccessToken, StringComparison.Ordinal))
                return true;

            await RefreshLock.WaitAsync(cancellationToken);
            try
            {
                // Kilit beklerken token yenilenmis olabilir; tekrar kontrol et.
                accessToken = await authStateProvider.GetTokenAsync();
                refreshToken = await authStateProvider.GetRefreshTokenAsync();

                if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken))
                    return false;

                if (!string.Equals(accessToken, failedAccessToken, StringComparison.Ordinal))
                    return true;

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
            finally
            {
                RefreshLock.Release();
            }
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
