using DotNetEnv;
using Microsoft.AspNetCore.Authentication.Cookies;
using WebServer.Interfaces;
using WebServer.Services;
Env.Load();
var builder = WebApplication.CreateBuilder(args);
// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddMemoryCache();
builder.Services.Configure<WebServer.Services.EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.Configure<WebServer.Services.SmtpOptions>(builder.Configuration.GetSection("Smtp"));
builder.Services.AddScoped<WebServer.Interfaces.IEmailSender, WebServer.Services.SmtpEmailSender>();
builder.Services.AddHttpClient<IAuthService, AuthService>(
    (sp, client) =>
    {
        var config = sp.GetRequiredService<IConfiguration>();
        var baseUrl = config["ApiClients:Auth:BaseUrl"] ?? "http://localhost:5007";
        client.BaseAddress = new Uri(baseUrl);
    });
builder.Services.AddHttpClient<IConversationService, ConversationService>(
    (sp, client) =>
    {
        var config = sp.GetRequiredService<IConfiguration>();
        var baseUrl = config["ApiClients:Conversations:BaseUrl"] ?? "http://localhost:5226";
        client.BaseAddress = new Uri(baseUrl);
    }
    );
builder.Services.AddHttpClient<IUserService, UserService>(
    (sp, client) =>
    {
        var config = sp.GetRequiredService<IConfiguration>();
        var baseUrl = config["ApiClients:Users:BaseUrl"] ?? "http://localhost:5183";
        client.BaseAddress = new Uri(baseUrl);
    });
builder.Services.AddHttpClient<IProfileService, ProfileService>(
    (sp, client) =>
    {
        var config = sp.GetRequiredService<IConfiguration>();
        var baseUrl = config["ApiClients:Users:BaseUrl"] ?? "http://localhost:5183";
        client.BaseAddress = new Uri(baseUrl);
    });
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opt => { opt.LoginPath = "/Auth/Login"; opt.LogoutPath = "/Auth/Logout"; opt.ExpireTimeSpan = TimeSpan.FromHours(8); opt.SlidingExpiration = true; });
builder.Services.AddSingleton<WebServer.Services.RealtimeHub>();
builder.Services.AddSingleton<WebServer.Services.GroupCallRegistry>();
builder.Services.AddSingleton<WebServer.Services.WebSocketHandler>();
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

app.UseAuthentication();
app.UseAuthorization();
app.UseWebSockets();
app.Map("/ws", async httpContext =>
{
    if (!httpContext.WebSockets.IsWebSocketRequest)
    {
        httpContext.Response.StatusCode = 400;
        await httpContext.Response.WriteAsync("WebSocket request required");
        return;
    }

    // lấy userId từ claims (cookie auth)
    string? userId = httpContext.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

    // fallback: query userId (nếu bạn vẫn muốn)
    if (string.IsNullOrWhiteSpace(userId))
        userId = httpContext.Request.Query["userId"].ToString();

    if (string.IsNullOrWhiteSpace(userId))
    {
        httpContext.Response.StatusCode = 401;
        await httpContext.Response.WriteAsync("Missing userId");
        return;
    }

    var ws = await httpContext.WebSockets.AcceptWebSocketAsync();
    var handler = httpContext.RequestServices.GetRequiredService<WebServer.Services.WebSocketHandler>();

    await handler.HandleAsync(ws, userId, httpContext.RequestAborted);
});

app.MapStaticAssets();
app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=Login}"
);

app.Run();
