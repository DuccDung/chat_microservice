using ApplicationServer.Dtos.Notifications;
using ApplicationServer.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ApplicationServer.Services;

public sealed class NotificationService : INotificationService
{
    private readonly SocialNetworkContext _context;

    public NotificationService(SocialNetworkContext context)
    {
        _context = context;
    }

    public async Task<NotificationDto> CreateAsync(CreateNotificationRequest req, CancellationToken ct = default)
    {
        if (req == null)
            throw new ServiceException(StatusCodes.Status400BadRequest, "Body is required.");
        if (string.IsNullOrWhiteSpace(req.Type))
            throw new ServiceException(StatusCodes.Status400BadRequest, "Type is required.");
        if (string.IsNullOrWhiteSpace(req.Content))
            throw new ServiceException(StatusCodes.Status400BadRequest, "Content is required.");
        if (req.SenderId <= 0 || req.ConsumerId <= 0)
            throw new ServiceException(StatusCodes.Status400BadRequest, "SenderId and ConsumerId are required.");

        var usersCount = await _context.Accounts
            .CountAsync(a => a.AccountId == req.SenderId || a.AccountId == req.ConsumerId, ct);

        var expectedUsersCount = req.SenderId == req.ConsumerId ? 1 : 2;
        if (usersCount != expectedUsersCount)
            throw new ServiceException(StatusCodes.Status404NotFound, "Sender or consumer not found.");

        var notification = new Notification
        {
            Type = req.Type.Trim(),
            Content = req.Content.Trim(),
            SenderId = req.SenderId,
            ConsumerId = req.ConsumerId,
            Date = DateTime.UtcNow,
            IsRead = false
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync(ct);

        return Map(notification);
    }

    public async Task<List<NotificationDto>> GetByConsumerAsync(int consumerId, int limit = 50, CancellationToken ct = default)
    {
        if (consumerId <= 0)
            throw new ServiceException(StatusCodes.Status400BadRequest, "consumerId is required.");

        limit = Math.Clamp(limit, 1, 200);

        return await _context.Notifications
            .AsNoTracking()
            .Where(n => n.ConsumerId == consumerId)
            .OrderByDescending(n => n.Date)
            .Take(limit)
            .Select(n => new NotificationDto
            {
                Id = n.Id,
                Type = n.Type,
                Content = n.Content,
                SenderId = n.SenderId,
                ConsumerId = n.ConsumerId,
                Date = n.Date,
                IsRead = n.IsRead
            })
            .ToListAsync(ct);
    }

    public async Task MarkReadAsync(int notificationId, int consumerId, CancellationToken ct = default)
    {
        if (notificationId <= 0 || consumerId <= 0)
            throw new ServiceException(StatusCodes.Status400BadRequest, "notificationId and consumerId are required.");

        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.ConsumerId == consumerId, ct);

        if (notification == null)
            throw new ServiceException(StatusCodes.Status404NotFound, "Notification not found.");

        notification.IsRead = true;
        await _context.SaveChangesAsync(ct);
    }

    private static NotificationDto Map(Notification notification)
    {
        return new NotificationDto
        {
            Id = notification.Id,
            Type = notification.Type,
            Content = notification.Content,
            SenderId = notification.SenderId,
            ConsumerId = notification.ConsumerId,
            Date = notification.Date,
            IsRead = notification.IsRead
        };
    }
}
