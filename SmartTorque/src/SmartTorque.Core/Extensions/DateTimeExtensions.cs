namespace SmartTorque.Core.Extensions;

public static class DateTimeExtensions
{
    // Indian Standard Time is strictly UTC + 5:30 (330 minutes) with no DST
    public static readonly TimeSpan IstOffset = TimeSpan.FromMinutes(330);

    public static DateTime ToIst(this DateTime dt)
    {
        var utc = dt.Kind switch
        {
            DateTimeKind.Local => dt.ToUniversalTime(),
            DateTimeKind.Utc => dt,
            _ => DateTime.SpecifyKind(dt, DateTimeKind.Utc)
        };
        return utc.Add(IstOffset);
    }

    public static DateTime? ToIst(this DateTime? dt)
    {
        return dt.HasValue ? dt.Value.ToIst() : null;
    }

    public static string ToIstString(this DateTime dt, string format = "yyyy-MM-dd HH:mm")
    {
        return dt.ToIst().ToString(format);
    }

    public static string ToIstString(this DateTime? dt, string format = "yyyy-MM-dd HH:mm", string fallback = "—")
    {
        return dt.HasValue ? dt.Value.ToIstString(format) : fallback;
    }
}