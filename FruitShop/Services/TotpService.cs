using System;
using OtpNet;
using QRCoder;

namespace FruitShop.Services
{
    /// <summary>
    /// Dịch vụ xác thực 2 yếu tố TOTP &amp; tạo mã QR chuẩn Google/Microsoft Authenticator.
    /// </summary>
    public class TotpService
    {
        public string GenerateSecretKey()
        {
            var key = KeyGeneration.GenerateRandomKey(20);
            return Base32Encoding.ToString(key);
        }

        public string BuildOtpAuthUri(string email, string secretKey, string issuer = "FruitShop")
        {
            var encodedIssuer = Uri.EscapeDataString(issuer);
            var encodedAccount = Uri.EscapeDataString(email);
            return $"otpauth://totp/{encodedIssuer}:{encodedAccount}?secret={secretKey}&issuer={encodedIssuer}&digits=6";
        }

        public string GenerateQrCodeDataUri(string otpAuthUri)
        {
            using var qrGenerator = new QRCodeGenerator();
            using var qrData = qrGenerator.CreateQrCode(otpAuthUri, QRCodeGenerator.ECCLevel.Q);
            using var pngQr = new PngByteQRCode(qrData);
            var bytes = pngQr.GetGraphic(8);
            return "data:image/png;base64," + Convert.ToBase64String(bytes);
        }

        public bool VerifyCode(string? secretKey, string? code)
        {
            if (string.IsNullOrWhiteSpace(secretKey) || string.IsNullOrWhiteSpace(code))
            {
                return false;
            }

            var cleanedCode = code.Trim().Replace(" ", "");
            if (cleanedCode.Length != 6)
            {
                return false;
            }

            try
            {
                var secretBytes = Base32Encoding.ToBytes(secretKey);
                var totp = new Totp(secretBytes);
                return totp.VerifyTotp(cleanedCode, out _, new VerificationWindow(previous: 1, future: 1));
            }
            catch
            {
                return false;
            }
        }
    }
}
