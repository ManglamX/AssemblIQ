using System;
using System.Collections.Generic;

namespace SmartTorque.Web.Models;

public partial class Notification
{
    public int NotificationId { get; set; }

    public byte Type { get; set; }

    public string Message { get; set; } = null!;

    public int? JobId { get; set; }

    public string TargetRole { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? ReadAt { get; set; }

    public virtual Job? Job { get; set; }
}