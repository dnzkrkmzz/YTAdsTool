using Microsoft.EntityFrameworkCore;
using YTReklamAraci.Data;

var builder = WebApplication.CreateBuilder(args);

// --- SERVİS KAYITLARI (MUTFAK AÇIK) ---

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite("Data Source=ytreklam.db"));

// --- YENİ HALİ (MVC Liste Sınırını Kaldırır) ---
builder.Services.AddControllersWithViews(options =>
{
    // Varsayılan 1024 olan liste bağlama sınırını sonsuza (veya ihtiyacımız olan büyüklüğe) çekiyoruz
    options.MaxModelBindingCollectionSize = int.MaxValue; 
});

// İŞTE BURASI: Excel için form limitini kaldıran ayar Build() edilmeden ÖNCE olmalı!
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.ValueCountLimit = int.MaxValue; // Sınırsız sayıda kutucuk kabul et
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartBodyLengthLimit = int.MaxValue;
});

// --- UYGULAMAYI İNŞA ET (MUTFAK KAPANDI) ---
var app = builder.Build();

// --- MIDDLEWARE VE ROUTİNG AYARLARI ---

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles(); // Static files routing'den önce gelmeli (best practice)
app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();