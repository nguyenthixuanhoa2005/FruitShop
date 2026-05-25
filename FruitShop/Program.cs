using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

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
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(); // Hỗ trợ tài liệu API UI

builder.Services.AddSingleton<FruitShop.Interfaces.ISearchService, FruitShop.Services.MeiliSearchService>();
builder.Services.AddScoped<FruitShop.Services.MeiliSearchSyncService>();

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
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Sử dụng Session trước Authorization
app.UseSession();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
