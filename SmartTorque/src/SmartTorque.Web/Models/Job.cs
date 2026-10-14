using System;
using System.Collections.Generic;

namespace SmartTorque.Web.Models;

public partial class Job
{
    public int JobId { get; set; }

    public string JobNo { get; set; } = null!;

    public int ProductId { get; set; }

    public string? ProductSerial { get; set; }

    public int Quantity { get; set; }

    public int QuantityCompleted { get; set; }

    public byte Priority { get; set; }

    public DateOnly? DueDate { get; set; }

    public byte AssignmentMode { get; set; }

    public string? AssignedWorkerId { get; set; }

    public byte Status { get; set; }

    public byte ExecutionMode { get; set; }

    public int? SourceBmRowId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual AspNetUser? AssignedWorker { get; set; }

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual Product Product { get; set; } = null!;

    public virtual BmRow? SourceBmRow { get; set; }
}