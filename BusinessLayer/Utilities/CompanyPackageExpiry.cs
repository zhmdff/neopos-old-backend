namespace BusinessLayer.Utilities;

/// <summary>Lisenziya (PackageEndDate) — Bakı təqvimi ilə.</summary>
public static class CompanyPackageExpiry
{
    private static readonly TimeZoneInfo BakuTz =
        TimeZoneInfo.FindSystemTimeZoneById("Azerbaijan Standard Time");

    public const string ExpiredMessageAz =
        "Lisenziyanın müddəti bitib. Yeniləmə üçün +994505738147 nömrəsi ilə əlaqə saxlayın.";

    private static DateTime ToUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    /// <summary>Bitmə tarixi (daxil) ilə bu gün arasında tam təqvim günləri: mənfi = müddət bitib.</summary>
    public static int GetRemainingCalendarDaysInBaku(DateTime packageEndDate)
    {
        var utc = ToUtc(packageEndDate);
        var endDay = TimeZoneInfo.ConvertTimeFromUtc(utc, BakuTz).Date;
        var today = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, BakuTz).Date;
        return (int)(endDay - today).Days;
    }

    public static bool IsExpired(DateTime packageEndDate) => GetRemainingCalendarDaysInBaku(packageEndDate) < 0;
}
