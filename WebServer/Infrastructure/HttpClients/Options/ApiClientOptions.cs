namespace WebServer.Infrastructure.HttpClients.Options
{
    public class ApiClientOptions
    {
        public string BaseUrl { get; set; } = "";
        public int TimeoutSeconds { get; set; } = 15;
    }
}
