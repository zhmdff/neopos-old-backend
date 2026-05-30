using BusinessLayer.DTOs.Print;
using Domain.Common.Entities;

namespace BusinessLayer.Printing;

public static class KassaReceiptContextMapper
{
    public static KassaReceiptContext FromDto(RenderKassaEscPosDto dto, Company company)
    {
        return new KassaReceiptContext
        {
            CompanyName = FirstNonEmpty(dto.CompanyName, company.NameAz) ?? "NEOPOS",
            CheckNumber = dto.CheckNumber,
            TableName = dto.TableName,
            HallName = dto.HallName,
            WaiterName = dto.WaiterName,
            KassirName = dto.Kassir,
            CustomerName = dto.CustomerName,
            CustomerPhone = dto.CustomerPhone,
            CustomerAddress = dto.CustomerAddress,
            GuestCount = dto.GuestCount,
            OpenTime = ParseDateTime(dto.OpenTime),
            CloseTime = ParseDateTime(dto.CloseTime),
            ExtraText = dto.ExtraText,
            ThankYouText = company.KassaReceiptThankYouText,
            SplitLabel = dto.SplitLabel,
            FoodTotal = dto.FoodTotal,
            ServiceAmount = dto.ServiceAmount,
            DiscountAmount = dto.DiscountAmount,
            GrandTotal = dto.GrandTotal,
            DepositLimit = dto.DepositLimit,
            IsPaid = dto.IsPaid,
            PaidCash = dto.PaidCash,
            PaidCard = dto.PaidCard,
            CustomPaymentMethodName = dto.CustomPaymentMethodName,
            Items = dto.Items.Select(i => new KassaReceiptLineItem
            {
                Name = i.Name ?? "",
                Qty = i.Qty,
                Price = i.Price,
                Total = i.Total,
                Note = i.Note,
            }).ToList(),
        };
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
        }

        return null;
    }

    private static DateTime? ParseDateTime(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (DateTime.TryParse(raw, out var dt)) return dt;
        return null;
    }
}
