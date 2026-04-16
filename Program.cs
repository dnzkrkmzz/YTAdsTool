using Microsoft.EntityFrameworkCore;
using YTReklamAraci.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite("Data Source=ytreklam.db"));

// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();
// YENİ EKLENEN KOD: Uygulamanın /yt altında çalışacağını belirtir
app.UsePathBase("/yt");

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.UseStaticFiles();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");


app.Run();
