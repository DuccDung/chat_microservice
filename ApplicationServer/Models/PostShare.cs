using System;
using System.Collections.Generic;

namespace ApplicationServer;

public partial class PostShare
{
    public int PsId { get; set; }

    public int PostId { get; set; }

    public int AccountId { get; set; }

    public virtual Post Post { get; set; } = null!;
}
