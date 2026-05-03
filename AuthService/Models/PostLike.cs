using System;
using System.Collections.Generic;

namespace AuthService;

public partial class PostLike
{
    public int LikeId { get; set; }

    public int? UserId { get; set; }

    public int? PostId { get; set; }

    public virtual Post? Post { get; set; }
}
