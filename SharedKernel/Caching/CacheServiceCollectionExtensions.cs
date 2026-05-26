using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Sockets;

namespace SharedKernel.Caching;

public static class CacheServiceCollectionExtensions
{
    public static IServiceCollection AddSafeDistributedCache(
        this IServiceCollection services,
        IConfiguration configuration,
        string defaultInstanceName)
    {
        var connectionString = GetSetting(configuration, "Redis:ConnectionString", "Redis__ConnectionString", "REDIS_CONNECTION");
        var instanceName = GetSetting(configuration, "Redis:InstanceName", "Redis__InstanceName", "REDIS_INSTANCE_NAME")
            ?? defaultInstanceName;

        if (ShouldUseNoopCache(connectionString))
        {
            services.AddSingleton<IDistributedCache, NoopDistributedCache>();
        }
        else
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = NormalizeRedisConnectionString(connectionString!);
                options.InstanceName = instanceName;
            });
        }

        services.AddSingleton<ICacheService, SafeCacheService>();
        return services;
    }

    private static bool ShouldUseNoopCache(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return true;

        var value = connectionString.Trim();
        if (value.Equals("none", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("noop", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("disabled", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IsUnavailableLocalRedis(value);
    }

    private static string NormalizeRedisConnectionString(string connectionString)
    {
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["abortConnect"] = "false",
            ["connectRetry"] = "1",
            ["connectTimeout"] = "500",
            ["syncTimeout"] = "500",
            ["asyncTimeout"] = "500"
        };

        foreach (var option in options)
        {
            if (!HasRedisOption(connectionString, option.Key))
                connectionString += $",{option.Key}={option.Value}";
        }

        return connectionString;
    }

    private static bool HasRedisOption(string connectionString, string optionName)
    {
        return connectionString
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(part => part.StartsWith(optionName + "=", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsUnavailableLocalRedis(string connectionString)
    {
        if (!TryGetFirstRedisEndpoint(connectionString, out var host, out var port))
            return false;

        var isLocalhost =
            host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
            host.Equals("::1", StringComparison.OrdinalIgnoreCase);

        if (!isLocalhost)
            return false;

        try
        {
            using var client = new TcpClient();
            var connectHost = host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ? "127.0.0.1" : host;
            var connectTask = client.ConnectAsync(connectHost, port);

            return !connectTask.Wait(TimeSpan.FromMilliseconds(150)) || !client.Connected;
        }
        catch
        {
            return true;
        }
    }

    private static bool TryGetFirstRedisEndpoint(string connectionString, out string host, out int port)
    {
        host = "";
        port = 6379;

        var endpoint = connectionString
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(part => !part.Contains('='));

        if (string.IsNullOrWhiteSpace(endpoint))
            return false;

        if (endpoint.StartsWith('['))
        {
            var endBracket = endpoint.IndexOf(']');
            if (endBracket <= 1)
                return false;

            host = endpoint[1..endBracket];
            var portPart = endpoint[(endBracket + 1)..].TrimStart(':');
            if (!string.IsNullOrWhiteSpace(portPart) && int.TryParse(portPart, out var parsedPort))
                port = parsedPort;

            return true;
        }

        var lastColon = endpoint.LastIndexOf(':');
        if (lastColon > 0 && int.TryParse(endpoint[(lastColon + 1)..], out var p))
        {
            host = endpoint[..lastColon];
            port = p;
            return true;
        }

        host = endpoint;
        return true;
    }

    private static string? GetSetting(IConfiguration configuration, string configKey, params string[] envKeys)
    {
        foreach (var envKey in envKeys)
        {
            var value = Environment.GetEnvironmentVariable(envKey);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return configuration[configKey];
    }
}
