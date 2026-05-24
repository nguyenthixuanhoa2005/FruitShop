using System.ComponentModel.DataAnnotations;

namespace Web_hoaqua.Models;

public class LoginViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập email")]
    [Display(Name = "Tên đăng nhập")]
    public string Email { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; }

    [Display(Name = "Nhớ tôi")]
    public bool RememberMe { get; set; }

    public string? ErrorMessage { get; set; }
}
