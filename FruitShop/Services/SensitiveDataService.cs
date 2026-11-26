using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace FruitShop.Services
{
    /// <summary>
    /// Mã hóa và giải mã dữ liệu cá nhân nhạy cảm (SĐT) bằng ASP.NET Core Data Protection API.
    /// </summary>
    public class SensitiveDataService
    {
        private readonly IDataProtector _protector;

        public SensitiveDataService(IDataProtectionProvider provider)
        {
            _protector = provider.CreateProtector("FruitShop.UserData.Phone.v1");
        }

        public string? Protect(string? plainText)
        {
            if (string.IsNullOrWhiteSpace(plainText))
            {
                return plainText;
            }

            var trimmed = plainText.Trim();
            if (trimmed.StartsWith("CfDJ8", StringComparison.Ordinal))
            {
                return trimmed;
            }

            return _protector.Protect(trimmed);
        }

        public string? Unprotect(string? cipherOrPlainText)
        {
            if (string.IsNullOrWhiteSpace(cipherOrPlainText))
            {
                return cipherOrPlainText;
            }

            try
            {
                return _protector.Unprotect(cipherOrPlainText);
            }
            catch (CryptographicException)
            {
                // Giữ tương thích ngược với dữ liệu SĐT cũ chưa được mã hóa trong DB
                return cipherOrPlainText;
            }
        }
    }
}
