using ApplicationServer;
using ApplicationServer.Services;
using ApplicationServer.Services.Interfaces;
using DotNetEnv;
using Microsoft.EntityFrameworkCore;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
Env.Load();
builder.Services.AddControllers();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IMessageCallService, MessageCallService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddDbContext<SocialNetworkContext>(options =>
    options.UseSqlServer(
        Environment.GetEnvironmentVariable("DB_Connection")
    )
);
builder.Services.AddCors(opt =>
{
    opt.AddPolicy("webServer", policy =>
    {
        policy.WithOrigins(Environment.GetEnvironmentVariable("WebServer_Origin") ?? "")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();
    });
});
var app = builder.Build();
app.UseCors("webServer");
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
