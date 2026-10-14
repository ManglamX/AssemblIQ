using System;
using System.Collections.Generic;

namespace SmartTorque.Web.Models;

public partial class Setting
{
    public int SettingId { get; set; }

    public string Key { get; set; } = null!;

    public string Value { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }
}