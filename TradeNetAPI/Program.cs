using TradeNetAPI.Data;
using TradeNetAPI.Interfaces;
using TradeNetAPI.Repositories;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container - API/Data Layer only
builder.Services.AddOpenApi();
builder.Services.AddControllers();

// Add CORS to allow Portal to call API
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowPortal", policy =>
    {
        policy.WithOrigins("https://localhost:7088")
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add DbContext with SQL Server
builder.Services.AddDbContext<TradeNetDbContext>(options =>
{
    // Option 1: InMemory Database 
    options.UseInMemoryDatabase("TradeNetDB");

    // Option 2: SQL Server
    // options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

// Register repositories
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IBusinessRepository, BusinessRepository>();
builder.Services.AddScoped<IBusinessDocumentRepository, BusinessDocumentRepository>();
builder.Services.AddScoped<ITradeLicenseRepository, TradeLicenseRepository>();
builder.Services.AddScoped<ILicenseDocumentRepository, LicenseDocumentRepository>();
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddScoped<ITradeProgramRepository, TradeProgramRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<IComplianceRecordRepository, ComplianceRecordRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<IMarketRecordRepository, MarketRecordRepository>();

var app = builder.Build();

// Seed initial data
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<TradeNetDbContext>();
    context.Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("AllowPortal");
app.MapControllers();

app.Run();
