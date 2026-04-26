using ApplicationServer.Models;
using System;
using System.Collections.Generic;

namespace ApplicationServer;

public partial class Message
{
    public int MessageId { get; set; }

    public int ConversationId { get; set; }

    public int SenderId { get; set; }

    public string? Content { get; set; }

    public string? MessageType { get; set; }

    public DateTime? CreatedAt { get; set; }

    public int? ParentMessageId { get; set; }

    public bool? IsRead { get; set; }

    public bool? IsRemove { get; set; }

    public virtual Conversation Conversation { get; set; } = null!;

    public virtual Account Sender { get; set; } = null!;
}
