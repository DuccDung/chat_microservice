using ApplicationServer.Models;
using System;
using System.Collections.Generic;

namespace ApplicationServer;

public partial class PostComment
{
    public int CommentId { get; set; }

    public int PostId { get; set; }

    public int AccountId { get; set; }

    public string Content { get; set; } = null!;

    public int? ParentCommentId { get; set; }

    public DateTime? CreateAt { get; set; }

    public virtual Account Account { get; set; } = null!;

    public virtual ICollection<PostComment> InverseParentComment { get; set; } = new List<PostComment>();

    public virtual PostComment? ParentComment { get; set; }

    public virtual Post Post { get; set; } = null!;
}
