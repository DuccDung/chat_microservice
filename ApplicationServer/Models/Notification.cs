using System;
using System.Collections.Generic;

namespace ApplicationServer;

public partial class Notification
{
    public int Id { get; set; }

    public string Type { get; set; } = null!;

    public string Content { get; set; } = null!;

    public int SenderId { get; set; }

    public int ConsumerId { get; set; }

    public DateTime? Date { get; set; }

    public bool? IsRead { get; set; }
}
