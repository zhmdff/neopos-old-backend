using Domain.Entities;

namespace BusinessLayer.Utilities;

public static class HallTimeDiscountHelper
{
    /// <summary>24 saat formatı; gecə keçən pəncərə dəstəyi (məs. 22:00–02:00).</summary>
    public static bool IsTimeInWindow(TimeSpan now, TimeSpan start, TimeSpan end)
    {
        if (start == end)
            return true;
        if (start < end)
            return now >= start && now <= end;
        return now >= start || now <= end;
    }

    public static HallTimeDiscountRule? PickActiveRule(IEnumerable<HallTimeDiscountRule> rules, DateTime localDateTime)
    {
        var now = localDateTime.TimeOfDay;
        return rules
            .Where(r => r.IsEnabled && !r.IsDeleted)
            .Where(r => IsTimeInWindow(now, r.StartTime, r.EndTime))
            .OrderByDescending(r => r.IsPercentageDiscount ? r.DiscountPercentage : r.DiscountAmount)
            .FirstOrDefault();
    }

    public static void ApplyToOrder(Domain.Entities.OrderHeader order, HallTimeDiscountRule rule)
    {
        order.IsPercentageDiscount = rule.IsPercentageDiscount;
        if (rule.IsPercentageDiscount)
        {
            order.DiscountPercentage = rule.DiscountPercentage;
            order.DiscountAmount = 0;
        }
        else
        {
            order.DiscountAmount = rule.DiscountAmount;
            order.DiscountPercentage = 0;
        }
    }
}
