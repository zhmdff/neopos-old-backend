namespace BusinessLayer.Helpers;

/// <summary>
/// Çek üzrə nağd/kart məbləğlərini ödəniləcək yekunla (TotalAmount − Beh) uyğunlaşdırır —
/// kassirin daxil etdiyi “artıq nağd” (qaytarıla) hesabatlarda satışdan böyük görünməsin.
/// </summary>
public static class OrderPaymentNet
{
    public static decimal EffectiveBehAmount(decimal totalAmount, decimal behAmount)
    {
        if (behAmount <= 0m) return 0m;
        return behAmount > totalAmount ? totalAmount : behAmount;
    }

    public static decimal PayableAmount(decimal totalAmount, decimal behAmount) =>
        Math.Max(0m, totalAmount - EffectiveBehAmount(totalAmount, behAmount));

    public static (decimal Cash, decimal Card) NormalizePaid(
        decimal totalAmount,
        decimal behAmount,
        decimal paidCash,
        decimal paidCard)
    {
        var payable = PayableAmount(totalAmount, behAmount);
        if (payable <= 0m)
            return (0m, 0m);

        var card = Math.Clamp(paidCard, 0m, payable);
        var maxCash = Math.Max(0m, payable - card);
        var cash = Math.Clamp(paidCash, 0m, maxCash);
        return (cash, card);
    }

    /// <summary>
    /// Ümumi hesabatlarda nağd/kart sütunları: əlavə ödəniş üsulu ilə bağlanmış çeklər bu cəmlərə daxil edilmir
    /// (məbləğ <c>CustomPaymentTotals</c> üzrə göstərilir).
    /// </summary>
    public static (decimal Cash, decimal Card) NaqdKartReportExcludingCustom(
        decimal totalAmount,
        decimal behAmount,
        decimal paidCash,
        decimal paidCard,
        Guid? customPaymentMethodId) =>
        customPaymentMethodId.HasValue ? (0m, 0m) : NormalizePaid(totalAmount, behAmount, paidCash, paidCard);

    /// <summary>
    /// Növbə/hesabat nağd-kart: ödənilmiş nağd+kart + xidmət ≈ yekun olduqda xidmət nağd/kart payına əlavə olunur
    /// (kassada alınan tam məbləğ; xidmət ayrıca sətirdə məlumat üçündür).
    /// </summary>
    public static (decimal Cash, decimal Card) NaqdKartReportGross(
        decimal totalAmount,
        decimal behAmount,
        decimal serviceAmount,
        decimal paidCash,
        decimal paidCard,
        Guid? customPaymentMethodId)
    {
        if (customPaymentMethodId.HasValue) return (0m, 0m);

        var (cash, card) = NormalizePaid(totalAmount, behAmount, paidCash, paidCard);
        var payable = PayableAmount(totalAmount, behAmount);
        var paid = cash + card;
        if (paid <= 0m || serviceAmount <= 0m) return (cash, card);

        const decimal eps = 0.02m;
        if (Math.Abs(paid + serviceAmount - payable) <= eps
            || (behAmount <= eps && Math.Abs(paid + serviceAmount - totalAmount) <= eps))
        {
            if (card <= eps) return (payable, 0m);
            var share = cash / paid;
            return (cash + serviceAmount * share, card + serviceAmount * (1m - share));
        }

        return (cash, card);
    }

    /// <summary>
    /// Yekun gəlir (çek cəmi) ilə nağd+kart+əlavə üsullar cəmi arasındakı kiçik fərqi (xidmət/beh/yuvarlaqlaşma) paylara paylayır.
    /// </summary>
    public static (decimal Cash, decimal Card) ReconcileReportPaymentTotals(
        decimal totalRevenue,
        decimal totalCash,
        decimal totalCard,
        decimal customPaymentsTotal)
    {
        var gap = totalRevenue - totalCash - totalCard - customPaymentsTotal;
        const decimal eps = 0.02m;
        if (gap <= eps) return (totalCash, totalCard);
        if (totalCard <= eps) return (totalCash + gap, totalCard);
        var denom = totalCash + totalCard;
        if (denom <= eps) return (totalCash + gap, totalCard);
        var share = totalCash / denom;
        return (totalCash + gap * share, totalCard + gap * (1m - share));
    }
}
