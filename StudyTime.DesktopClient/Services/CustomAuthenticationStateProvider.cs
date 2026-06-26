using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Maui.Storage;
using StudyTime.DesktopClient;
using StudyTime.DesktopClient.Offline;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Json;
using System.Diagnostics;

namespace StudyTime.DesktopClient.Services
{
    public class CustomAuthenticationStateProvider : AuthenticationStateProvider
    {
        private static readonly TimeSpan StorageTimeout = TimeSpan.FromSeconds(2);
        private const string TokenKey = "jwt_token";
        private const string RefreshTokenKey = "jwt_refresh_token";
        private const string LocalOwnerSubKey = "studytime_local_owner_sub";
        private const string LocalProfileSubKey = "studytime_local_profile_sub";

        private readonly StudyTimeAppOptions _appOptions;
        private readonly LocalUserContext _localUserContext;
        private readonly IServiceProvider _services;

        public CustomAuthenticationStateProvider(
            StudyTimeAppOptions appOptions,
            LocalUserContext localUserContext,
            IServiceProvider services)
        {
            _appOptions       = appOptions;
            _localUserContext = localUserContext;
            _services         = services;
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            if (MauiProgram.IsOfflineBeta)
                return await Task.FromResult(CreateOfflineBetaAuthenticationState());

            if (_appOptions.LocalOnlyMode)
                return await GetLocalOnlyAuthenticationStateAsync();

            var token = await SafeGetAsync(TokenKey);

            if (string.IsNullOrWhiteSpace(token))
            {
                // Otomatik oturum dusmelerinde (token yok) local cache gorunurlugunu koru.
                var ownerSub = await SafeGetAsync(LocalOwnerSubKey);
                if (!string.IsNullOrWhiteSpace(ownerSub))
                    _localUserContext.SetUserId(ownerSub);
                else
                    _localUserContext.Clear();
                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
            }

            var claims = ParseClaimsFromJwt(token);

            var sub = claims.FirstOrDefault(c => c.Type == "sub")?.Value;
            if (!string.IsNullOrEmpty(sub))
                _localUserContext.SetUserId(sub);

            var identity = new ClaimsIdentity(claims, "jwt");
            var user = new ClaimsPrincipal(identity);

            return new AuthenticationState(user);
        }

        public async Task MarkUserAsAuthenticated(string token, string refreshToken)
        {
            UserInitiatedLogout = false; // Yeni giriş → "başka cihaz" tespitini tekrar etkinleştir.
            SafeRemove(LocalProfileSubKey);

            var claims = ParseClaimsFromJwt(token);
            var sub    = claims.FirstOrDefault(c => c.Type == "sub")?.Value;

            if (!string.IsNullOrEmpty(sub))
            {
                var previous = await SafeGetAsync(LocalOwnerSubKey);
                if (!string.IsNullOrEmpty(previous) && previous != sub)
                    await GetLocalDataWipeService().WipeAllUserLocalDataAsync();

                await SafeSetAsync(LocalOwnerSubKey, sub);
                _localUserContext.SetUserId(sub);
            }

            await SafeSetAsync(TokenKey, token);
            if (!string.IsNullOrEmpty(refreshToken))
            {
                await SafeSetAsync(RefreshTokenKey, refreshToken);
            }

            var identity = new ClaimsIdentity(claims, "jwt");
            var user = new ClaimsPrincipal(identity);

            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(user)));
        }

        public async Task MarkUserAsLoggedOut(bool wipeLocalData = true)
        {
            if (wipeLocalData)
                await GetLocalDataWipeService().WipeAllUserLocalDataAsync();
            SafeRemove(TokenKey);
            SafeRemove(RefreshTokenKey);
            if (wipeLocalData)
            {
                SafeRemove(LocalOwnerSubKey);
                SafeRemove(LocalProfileSubKey);
            }
            if (wipeLocalData)
                _localUserContext.Clear();

            if (MauiProgram.IsOfflineBeta)
            {
                NotifyAuthenticationStateChanged(Task.FromResult(CreateOfflineBetaAuthenticationState()));
                return;
            }

            var anonymousUser = new ClaimsPrincipal(new ClaimsIdentity());
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(anonymousUser)));
        }

        public async Task<string?> GetTokenAsync()
        {
            if (MauiProgram.IsOfflineBeta)
                return null;

            if (_appOptions.LocalOnlyMode)
                return null;

            return await SafeGetAsync(TokenKey);
        }

        public async Task<string?> GetRefreshTokenAsync()
        {
            if (MauiProgram.IsOfflineBeta)
                return null;

            if (_appOptions.LocalOnlyMode)
                return null;

            return await SafeGetAsync(RefreshTokenKey);
        }

        private AuthenticationState CreateOfflineBetaAuthenticationState()
        {
            const string sub = "00000000-0000-0000-0000-000000000001";
            _localUserContext.SetUserId(sub);

            var expUnix = DateTimeOffset.UtcNow.AddYears(50).ToUnixTimeSeconds();
            var premium = DateTime.UtcNow.AddYears(50);
            var claims = new List<Claim>
            {
                new("sub", sub),
                new(ClaimTypes.NameIdentifier, sub),
                new("UserId", sub),
                new(ClaimTypes.Name, "Beta Tester"),
                new("IsPremium", "true"),
                new("exp", expUnix.ToString()),
                new("PremiumUntil", premium.ToString("o"))
            };

            var identity = new ClaimsIdentity(claims, "offline-beta");
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }

        private async Task<AuthenticationState> GetLocalOnlyAuthenticationStateAsync()
        {
            var sub = await GetOrCreateLocalProfileSubAsync();
            _localUserContext.SetUserId(sub);

            var expUnix = DateTimeOffset.UtcNow.AddYears(50).ToUnixTimeSeconds();
            var premium = DateTime.UtcNow.AddYears(50);
            var claims = new List<Claim>
            {
                new("sub", sub),
                new(ClaimTypes.NameIdentifier, sub),
                new(ClaimTypes.Name, "Yerel kullanıcı"),
                new("exp", expUnix.ToString()),
                new("PremiumUntil", premium.ToString("o"))
            };

            var identity = new ClaimsIdentity(claims, "local");
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }

        private static async Task<string> GetOrCreateLocalProfileSubAsync()
        {
            var existing = await SafeGetAsync(LocalProfileSubKey);
            if (!string.IsNullOrEmpty(existing))
                return existing;

            var id = Guid.NewGuid().ToString("N");
            await SafeSetAsync(LocalProfileSubKey, id);
            return id;
        }

        private static string? _cachedToken;
        private static string? _cachedRefreshToken;

        /// <summary>
        /// Kullanıcı KENDİ isteğiyle çıkış yaptığında true olur. Çıkıştan sonra arka plan/gecikmeli
        /// isteklerden gelen SESSION_MISMATCH 401'lerinin "başka cihazda açıldı" mesajıyla
        /// gösterilmesini engellemek için kullanılır. Bir sonraki başarılı girişte sıfırlanır.
        /// </summary>
        public static bool UserInitiatedLogout { get; set; }

        private static async Task<string?> SafeGetAsync(string key)
        {
            if (key == TokenKey && _cachedToken != null) return _cachedToken;
            if (key == RefreshTokenKey && _cachedRefreshToken != null) return _cachedRefreshToken;

            try
            {
                var getTask = SecureStorage.Default.GetAsync(key);
                var completed = await Task.WhenAny(getTask, Task.Delay(StorageTimeout));
                if (completed == getTask)
                {
                    var val = await getTask;
                    if (key == TokenKey) _cachedToken = val;
                    if (key == RefreshTokenKey) _cachedRefreshToken = val;
                    return val;
                }

                Debug.WriteLine($"SecureStorage timeout on get: {key}, falling back to Preferences.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SecureStorage get failed for {key}: {ex.Message}");
            }

            var pref = Preferences.Default.Get<string?>(key, null);
            if (key == TokenKey) _cachedToken = pref;
            if (key == RefreshTokenKey) _cachedRefreshToken = pref;
            return pref;
        }

        private static async Task SafeSetAsync(string key, string value)
        {
            if (key == TokenKey) _cachedToken = value;
            if (key == RefreshTokenKey) _cachedRefreshToken = value;

            try
            {
                var setTask = SecureStorage.Default.SetAsync(key, value);
                var completed = await Task.WhenAny(setTask, Task.Delay(StorageTimeout));
                if (completed == setTask)
                {
                    await setTask;
                    Preferences.Default.Remove(key);
                    return;
                }

                Debug.WriteLine($"SecureStorage timeout on set: {key}, falling back to Preferences.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SecureStorage set failed for {key}: {ex.Message}");
            }

            Preferences.Default.Set(key, value);
        }

        private static void SafeRemove(string key)
        {
            if (key == TokenKey) _cachedToken = null;
            if (key == RefreshTokenKey) _cachedRefreshToken = null;

            try
            {
                SecureStorage.Default.Remove(key);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SecureStorage remove failed for {key}: {ex.Message}");
            }

            Preferences.Default.Remove(key);
        }

        private LocalDataWipeService GetLocalDataWipeService()
            => _services.GetRequiredService<LocalDataWipeService>();

        private IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
        {
            var payload = jwt.Split('.')[1];
            var jsonBytes = ParseBase64WithoutPadding(payload);
            var keyValuePairs = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonBytes);
            var claims = keyValuePairs!.Select(kvp => new Claim(kvp.Key, kvp.Value.ToString()!)).ToList();
            
            // Map "sub" to ClaimTypes.NameIdentifier
            var subClaim = claims.FirstOrDefault(c => c.Type == "sub");
            if (subClaim != null && !claims.Any(c => c.Type == ClaimTypes.NameIdentifier))
            {
                claims.Add(new Claim(ClaimTypes.NameIdentifier, subClaim.Value));
            }
            
            return claims;
        }

        private byte[] ParseBase64WithoutPadding(string base64)
        {
            switch (base64.Length % 4)
            {
                case 2: base64 += "=="; break;
                case 3: base64 += "="; break;
            }
            return Convert.FromBase64String(base64);
        }
    }
}
