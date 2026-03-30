using TradeNetPortal.Services;
using TradeNetPortal.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Add DbContext with SQL Server

builder.Services.AddDbContext<TradeNetDbContext>(options =>
{
    // Option 1: Use InMemory Database (for development/testing)
    options.UseInMemoryDatabase("TradeNetDB");

    // Option 2: Use SQL Server (uncomment and configure connection string in appsettings.json)
    // options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

// Keep the in-memory user store for now, but we'll update controllers to use DbContext
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

// Seed initial data
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<TradeNetDbContext>();
    context.Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseSession();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");


app.Run();
