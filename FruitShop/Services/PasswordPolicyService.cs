using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FruitShop.Models;
using Microsoft.EntityFrameworkCore;

namespace FruitShop.Services
{
    /// <summary>
    /// Kiểm tra chính sách mật khẩu mạnh và lịch sử 5 mật khẩu gần nhất (Chương 9).
    /// </summary>
    public class PasswordPolicyService
    {
        private readonly FruitShopContext _context;
        public const int MinimumLength = 12;
        public const int MinimumUniqueChars = 4;
        public const int HistoryLimit = 3;

        private static readonly string[] AdminForbiddenWords =
        {
            "admin", "administrator", "root", "password", "passw0rd",
            "fruitshop", "123456", "12345678", "qwerty", "letmein", "welcome"
        };

        public PasswordPolicyService(FruitShopContext context)
        {
            _context = context;
        }

        public bool ValidateStrength(string? password, out string errorMessage, bool isAdmin = false)
        {
            if (string.IsNullOrEmpty(password) || password.Length < MinimumLength)
            {
                errorMessage = $"Mật khẩu phải có ít nhất {MinimumLength} ký tự.";
                return false;
            }

            if (!password.Any(char.IsUpper))
            {
                errorMessage = "Mật khẩu phải chứa ít nhất 1 chữ cái viết hoa (A-Z).";
                return false;
            }

            if (!password.Any(char.IsLower))
            {
                errorMessage = "Mật khẩu phải chứa ít nhất 1 chữ cái viết thường (a-z).";
                return false;
            }

            if (!password.Any(char.IsDigit))
            {
                errorMessage = "Mật khẩu phải chứa ít nhất 1 chữ số (0-9).";
                return false;
            }

            if (!password.Any(c => !char.IsLetterOrDigit(c)))
            {
                errorMessage = "Mật khẩu phải chứa ít nhất 1 ký tự đặc biệt (ví dụ: !@#$%^&*).";
                return false;
            }

            if (password.Distinct().Count() < MinimumUniqueChars)
            {
                errorMessage = $"Mật khẩu phải chứa ít nhất {MinimumUniqueChars} ký tự khác nhau.";
                return false;
            }

            if (isAdmin)
            {
                var matchedWord = AdminForbiddenWords.FirstOrDefault(w =>
                    password.Contains(w, StringComparison.OrdinalIgnoreCase));
                if (matchedWord != null)
                {
                    errorMessage = $"Mật khẩu quản trị không được chứa từ dễ đoán (\"{matchedWord}\").";
                    return false;
                }
            }

            errorMessage = string.Empty;
            return true;
        }

        public async Task<bool> WasUsedBeforeAsync(int userId, string newPassword, string? currentPasswordHash = null)
        {
            var recentHashes = await _context.PasswordHistories
                .Where(h => h.UserId == userId)
                .OrderByDescending(h => h.CreatedAt)
                .Take(HistoryLimit)
                .Select(h => h.PasswordHash)
                .ToListAsync();

            var hashesToCheck = new List<string>(recentHashes);
            if (!string.IsNullOrEmpty(currentPasswordHash) && !hashesToCheck.Contains(currentPasswordHash))
            {
                hashesToCheck.Add(currentPasswordHash);
            }

            foreach (var hash in hashesToCheck)
            {
                if (VerifyAgainstHash(newPassword, hash))
                {
                    return true;
                }
            }

            return false;
        }

        public Task RecordPasswordAsync(int userId, string passwordHash)
        {
            _context.PasswordHistories.Add(new PasswordHistory
            {
                UserId = userId,
                PasswordHash = passwordHash,
                CreatedAt = DateTime.UtcNow
            });

            return Task.CompletedTask;
        }

        private static bool VerifyAgainstHash(string plainPassword, string storedHash)
        {
            try
            {
                return BCrypt.Net.BCrypt.Verify(plainPassword, storedHash);
            }
            catch (BCrypt.Net.SaltParseException)
            {
                return storedHash == plainPassword;
            }
        }
    }
}
