using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);


// Cấu hình HSTS
builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
    options.Preload = false;

    //Thêm cái này để chạy test hsts trên localhost
    options.ExcludedHosts.Clear();
});

// Add services to the container.
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        // Tránh lỗi vòng lặp khi trả về dữ liệu có quan hệ (như Category -> Parent -> Children)
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        // Chấp nhận cả camelCase và PascalCase từ Client
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });

// Cấu hình Session cho Shop

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);

    // Bảo vệ cookie Session
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
});


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(); // Hỗ trợ tài liệu API UI

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<FruitShop.Services.IpBanService>();
builder.Services.AddSingleton<FruitShop.Services.TotpService>();
builder.Services.AddSingleton<FruitShop.Interfaces.ISearchService, FruitShop.Services.MeiliSearchService>();
builder.Services.AddScoped<FruitShop.Services.MeiliSearchSyncService>();
builder.Services.AddScoped<FruitShop.Services.PasswordPolicyService>();

// Cho fetch + multipart (FormData): nhận token từ header RequestVerificationToken (khớp với JS trên Products/Index).
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
});

builder.Services.AddDbContext<FruitShop.Models.FruitShopContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("MyProductDb")));

var app = builder.Build();


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseExceptionHandler("/Home/Error");
}

// HSTS chỉ bật ngoài môi trường Development
//if (!app.Environment.IsDevelopment())
//{
 app.UseHsts();
//}

// Chuyển hướng HTTP sang HTTPS
app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseRouting();

// Sử dụng Session trước Authorization
app.UseSession();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// AdminSeed: đọc từ User Secrets hoặc biến môi trường (không hard-code mật khẩu)
using (var scope = app.Services.CreateScope())
{
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var adminEmail = config["AdminSeed:Email"];
    var adminPassword = config["AdminSeed:Password"];

    if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
    {
        var db = scope.ServiceProvider.GetRequiredService<FruitShop.Models.FruitShopContext>();
        var exists = db.Users.Any(u => u.Email == adminEmail && u.DeletedAt == null);
        if (!exists)
        {
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword);
            var adminUser = new FruitShop.Models.User
            {
                Email = adminEmail.Trim(),
                FullName = "Administrator",
                PasswordHash = passwordHash,
                RoleId = 1,
                Status = 1,
                IsVerified = 1,
                CreatedAt = DateTime.UtcNow,
                LastPasswordChangedAt = DateTime.UtcNow
            };
            db.Users.Add(adminUser);
            db.SaveChanges();

            db.PasswordHistories.Add(new FruitShop.Models.PasswordHistory
            {
                UserId = adminUser.Id,
                PasswordHash = passwordHash,
                CreatedAt = DateTime.UtcNow
            });
            db.SaveChanges();
        }
    }
}

app.Run();
