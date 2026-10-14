using System;
using System.Collections.Generic;

namespace SmartTorque.Web.Models;

public partial class AuditLog
{
    public long AuditLogId { get; set; }

    public DateTime OccurredAt { get; set; }

    public string? UserId { get; set; }

    public string? UserName { get; set; }

    public string EntityName { get; set; } = null!;

    public string? EntityId { get; set; }

    public string Action { get; set; } = null!;

    public string? Changes { get; set; }
}