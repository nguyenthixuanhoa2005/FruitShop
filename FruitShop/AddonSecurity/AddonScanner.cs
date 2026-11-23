using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace FruitShop.AddonSecurity
{
    public record ScanFinding(string File, int Line, string Rule, string Snippet);

    public class AddonScanner
    {
        private static readonly (string Rule, Regex Pattern)[] Rules =
        {
            ("Thẻ <script>",              new(@"<\s*script\b", RegexOptions.IgnoreCase)),
            ("Thuộc tính sự kiện JS",     new(@"\son\w+\s*=", RegexOptions.IgnoreCase)),
            ("eval / new Function",       new(@"\beval\s*\(|new\s+Function\s*\(", RegexOptions.IgnoreCase)),
            ("Giải mã base64",            new(@"atob\s*\(|FromBase64String", RegexOptions.IgnoreCase)),
            ("Chạy tiến trình hệ thống",  new(@"Process\.Start|cmd\.exe|powershell", RegexOptions.IgnoreCase)),
            ("Nạp assembly động",         new(@"Assembly\.(Load|LoadFrom)", RegexOptions.IgnoreCase)),
            ("Gọi URL bên ngoài",         new(@"https?://(?!localhost)[\w\.-]+", RegexOptions.IgnoreCase)),
        };

        private static readonly string[] Exts =
            { ".js", ".html", ".htm", ".cshtml", ".cs", ".config", ".svg", ".aspx", ".ashx" };

        public List<ScanFinding> ScanDirectory(string dir)
        {
            var result = new List<ScanFinding>();
            foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
                         .Where(f => Exts.Contains(Path.GetExtension(f).ToLowerInvariant())))
            {
                var lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    foreach (var (rule, pattern) in Rules)
                    {
                        if (pattern.IsMatch(lines[i]))
                        {
                            var trimmed = lines[i].Trim();
                            result.Add(new ScanFinding(
                                Path.GetRelativePath(dir, file),
                                i + 1,
                                rule,
                                trimmed.Length > 120 ? trimmed[..120] : trimmed));
                        }
                    }
                }
            }
            return result;
        }
    }
}
