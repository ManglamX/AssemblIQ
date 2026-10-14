using System;
using System.Collections.Generic;

namespace SmartTorque.Web.Models;

public partial class BmUpload
{
    public int UploadId { get; set; }

    public string FileName { get; set; } = null!;

    public string StoredPath { get; set; } = null!;

    public string? UploadedBy { get; set; }

    public DateTime UploadedAt { get; set; }

    public byte Status { get; set; }

    public int RowCount { get; set; }

    public int ErrorCount { get; set; }

    public virtual ICollection<BmRow> BmRows { get; set; } = new List<BmRow>();
}