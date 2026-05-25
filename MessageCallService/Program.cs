using AuthService;
using AuthService.Services;
using AuthService.Services.Interfaces;
using DotNetEnv;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
LoadEnvironment(builder.Environment.ContentRootPath);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddScoped<IMessageCallService, AuthService.Services.MessageCallService>();

var connectionString =
    Environment.GetEnvironmentVariable("DB_Connection")
    ?? builder.Configuration.GetConnectionString("sql_server");

if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("Missing DB_Connection environment variable or ConnectionStrings:sql_server.");

builder.Services.AddDbContext<SocialNetworkContext>(options =>
    options.UseSqlServer(connectionString));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.UseAuthorization();
app.MapControllers();
app.Run();

static void LoadEnvironment(string contentRootPath)
{
    var envPaths = new[]
    {
        Path.Combine(contentRootPath, ".env"),
        Path.GetFullPath(Path.Combine(contentRootPath, "..", ".env")),
        Path.GetFullPath(Path.Combine(contentRootPath, "..", "AuthService", ".env"))
    };

    foreach (var envPath in envPaths.Where(File.Exists))
    {
        Env.Load(envPath);
    }
}
