namespace BusinessLayer.Utilities;

/// <summary>
/// AuditLogs və CashShift kimi cədvəllərdə vaxt Bakı divar saatı kimi (DateTimeKind.Unspecified) saxlanır.
/// Terminal/API isə tez-tez ISO UTC (…Z) göndərir. Bu helper həmin parametrləri DB ilə müqayisə üçün
/// eyni konvensiyaya salır.
/// </summary>
public static class ReportQueryBakuTime
{
    private static readonly TimeZoneInfo BakuTz =
        TimeZoneInfo.FindSystemTimeZoneById("Azerbaijan Standard Time");

    public static DateTime ToBakuWallForDbComparison(DateTime value)
    {
        switch (value.Kind)
        {
            case DateTimeKind.Utc:
                var fromUtc = TimeZoneInfo.ConvertTimeFromUtc(value, BakuTz);
                return DateTime.SpecifyKind(fromUtc, DateTimeKind.Unspecified);
            case DateTimeKind.Local:
                var utc = value.ToUniversalTime();
                var fromLocal = TimeZoneInfo.ConvertTimeFromUtc(utc, BakuTz);
                return DateTime.SpecifyKind(fromLocal, DateTimeKind.Unspecified);
            default:
                return DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
        }
    }
}
