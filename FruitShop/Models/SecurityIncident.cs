using System;
using System.ComponentModel.DataAnnotations;

namespace FruitShop.Models;

public class SecurityIncident
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập người báo cáo / email")]
    [StringLength(150)]
    public string ReportedBy { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập tiêu đề sự cố")]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng mô tả chi tiết sự cố")]
    public string Description { get; set; } = string.Empty;

    [Required]
    [StringLength(30)]
    public string Severity { get; set; } = "Thấp";

    [StringLength(30)]
    public string Status { get; set; } = "Mới";

    [StringLength(150)]
    public string? AssignedTo { get; set; }

    public string? ResolutionNote { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ResolvedAt { get; set; }
}
