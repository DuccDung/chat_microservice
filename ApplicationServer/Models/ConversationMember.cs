using ApplicationServer.Models;
using System;
using System.Collections.Generic;

namespace ApplicationServer;

public partial class ConversationMember
{
    public int ConversationId { get; set; }

    public int AccountId { get; set; }

    public DateTime? JoinedAt { get; set; }

    public string? Title { get; set; }

    public DateTime? CreatedAt { get; set; }

    public int ConversationMemberId { get; set; }

    public virtual Account Account { get; set; } = null!;

    public virtual Conversation Conversation { get; set; } = null!;
}
