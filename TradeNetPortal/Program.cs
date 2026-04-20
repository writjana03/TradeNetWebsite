using TradeNetAPI.Services;
using TradeNetAPI.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container - UI ONLY
builder.Services.AddRazorPages();
builder.Services.AddControllersWithViews();

// Register HttpClient for API calls (pointing to TradeNetAPI)
builder.Services.AddHttpClient("TradeNetAPI", client =>
{
    client.BaseAddress = new Uri("https://localhost:7265");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// Add in-memory user store for UI authentication
builder.Services.AddSingleton<InMemoryUserStore>();

// Add Session support
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseSession();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
