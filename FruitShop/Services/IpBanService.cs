using System;
using Microsoft.Extensions.Caching.Memory;

namespace FruitShop.Services
{
    /// <summary>
    /// Chống dò quét mật khẩu theo địa chỉ IP bằng bộ nhớ đệm (Chương 9).
    /// </summary>
    public class IpBanService
    {
        private readonly IMemoryCache _cache;

        public const int MaxFailures = 10;
        public static readonly TimeSpan FailureWindow = TimeSpan.FromMinutes(10);
        public static readonly TimeSpan BanDuration = TimeSpan.FromHours(1);

        public IpBanService(IMemoryCache cache)
        {
            _cache = cache;
        }

        public bool IsBanned(string ip) => _cache.TryGetValue("ban:" + ip, out _);

        public void Ban(string ip, TimeSpan duration) => _cache.Set("ban:" + ip, true, duration);

        public int RegisterFailure(string ip, TimeSpan window)
        {
            var count = _cache.Get<int>("fail:" + ip) + 1;
            _cache.Set("fail:" + ip, count, window);
            return count;
        }

        public void ResetFailures(string ip) => _cache.Remove("fail:" + ip);
    }
}
