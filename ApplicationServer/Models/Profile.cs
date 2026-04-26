using ApplicationServer.Models;
using System;
using System.Collections.Generic;

namespace ApplicationServer;

public partial class Profile
{
    public int ProfileId { get; set; }

    public int UserId { get; set; }

    public string FullName { get; set; } = null!;

    public string? Phone { get; set; }

    public string? Gender { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public string? AvatarUrl { get; set; }

    public string? Address { get; set; }

    public string? Bio { get; set; }

    public DateTime? CreateAt { get; set; }

    public DateTime? UpdateAt { get; set; }

    public virtual Account User { get; set; } = null!;
}
