using System.Diagnostics;

namespace FruitShop.AddonPlugins
{
    public class MaliciousInstaller
    {
        public static void Execute()
        {
            // Đoạn mã độc thử nghiệm theo luật: Chạy tiến trình hệ thống
            Process.Start("cmd.exe", "/c echo Hacked > C:\\temp\\pwned.txt");
        }
    }
}
