namespace SharedKernel.Caching;

public static class CacheKeys
{
    public static string UserById(int accountId) => $"user:id:{accountId}";

    public static string UserByEmail(string email) => $"user:email:{NormalizeEmail(email)}";

    public static string Profile(int accountId) => $"profile:{accountId}";

    public static string ProfilePosts(int accountId) => $"profile:posts:{accountId}";

    public static string ConversationThreads(int accountId) => $"conv:threads:{accountId}";

    public static string ConversationMessagesVersion(int conversationId) => $"conv:messages:version:{conversationId}";

    public static string ConversationMessages(int conversationId, int limit, int? beforeMessageId, long version)
    {
        var before = beforeMessageId.HasValue ? beforeMessageId.Value.ToString() : "none";
        return $"conv:messages:{conversationId}:limit:{limit}:before:{before}:v:{version}";
    }

    public static string GroupInfoVersion(int conversationId) => $"group:info:version:{conversationId}";

    public static string GroupInfo(int conversationId, int meId, long version)
    {
        return $"group:info:{conversationId}:me:{meId}:v:{version}";
    }

    public static string ConversationPeer(int conversationId, int meId)
    {
        return $"conv:peer:{conversationId}:me:{meId}";
    }

    public static string NotificationsVersion(int consumerId) => $"notifications:version:{consumerId}";

    public static string Notifications(int consumerId, int limit, long version)
    {
        return $"notifications:{consumerId}:limit:{limit}:v:{version}";
    }

    private static string NormalizeEmail(string email)
    {
        return (email ?? "").Trim().ToLowerInvariant();
    }
}
