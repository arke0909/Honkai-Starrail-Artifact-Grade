using StackExchange.Redis;

namespace ArtifactGrade.Api;

internal static class RedisConnectionConfiguration
{
    public static ConfigurationOptions Parse(string connectionString)
    {
        if (!Uri.TryCreate(connectionString, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "redis" && uri.Scheme != "rediss"))
        {
            return ConfigurationOptions.Parse(connectionString);
        }

        var useTls = uri.Scheme == "rediss";
        var configuration = new ConfigurationOptions
        {
            AbortOnConnectFail = false,
            AsyncTimeout = 1000,
            ConnectTimeout = 1000,
            Ssl = useTls,
            SyncTimeout = 1000
        };
        configuration.EndPoints.Add(uri.Host, uri.IsDefaultPort ? (useTls ? 6380 : 6379) : uri.Port);

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            var credentials = uri.UserInfo.Split(':', 2);
            configuration.User = Uri.UnescapeDataString(credentials[0]);
            if (credentials.Length == 2)
            {
                configuration.Password = Uri.UnescapeDataString(credentials[1]);
            }
        }

        return configuration;
    }
}
