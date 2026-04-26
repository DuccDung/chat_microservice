namespace ApplicationServer.Dtos.Notifications;

public class NotificationDto
{
    public int Id { get; set; }
    public string Type { get; set; } = "";
    public string Content { get; set; } = "";
    public int SenderId { get; set; }
    public int ConsumerId { get; set; }
    public DateTime? Date { get; set; }
    public bool? IsRead { get; set; }
}

public class CreateNotificationRequest
{
    public string Type { get; set; } = "";
    public string Content { get; set; } = "";
    public int SenderId { get; set; }
    public int ConsumerId { get; set; }
}

