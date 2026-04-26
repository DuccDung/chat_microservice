using ApplicationServer.Dtos.Notifications;

namespace ApplicationServer.Services.Interfaces;

public interface INotificationService
{
    Task<NotificationDto> CreateAsync(CreateNotificationRequest req, CancellationToken ct = default);
    Task<List<NotificationDto>> GetByConsumerAsync(int consumerId, int limit = 50, CancellationToken ct = default);
    Task MarkReadAsync(int notificationId, int consumerId, CancellationToken ct = default);
}

