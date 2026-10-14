using System;
using System.Collections.Generic;

namespace SmartTorque.Web.Models;

public partial class BmRow
{
    public int RowId { get; set; }

    public int UploadId { get; set; }

    public int ExcelRowNo { get; set; }

    public string ProductCode { get; set; } = null!;

    public int Quantity { get; set; }

    public DateOnly? DueDate { get; set; }

    public string? ColourHex { get; set; }

    public byte Priority { get; set; }

    public string? Remarks { get; set; }

    public byte ImportStatus { get; set; }

    public string? ImportMessage { get; set; }

    public virtual ICollection<Job> Jobs { get; set; } = new List<Job>();

    public virtual BmUpload Upload { get; set; } = null!;
}