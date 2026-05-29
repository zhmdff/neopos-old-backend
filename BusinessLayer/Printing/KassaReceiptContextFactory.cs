using Domain.Common.Entities;
using Domain.Entities;

namespace BusinessLayer.Printing;

public static class KassaReceiptContextFactory
{
    public static KassaReceiptContext From(Company company, OrderHeader order, IList<OrderDetail> details)
    {
        var activeDetails = details.Where(d => d.Quantity > 0).ToList();
        var foodTotal = activeDetails.Sum(d => d.TotalPrice);
        var grandTotal = PayableAmount(order);

        return new KassaReceiptContext
        {
            CompanyName = company.NameAz ?? "NEOPOS",
            CheckNumber = order.CheckNumber,
            TableName = order.Table?.NameAz,
            HallName = order.Table?.Hall?.NameAz,
            WaiterName = order.WaiterName,
            KassirName = order.CashierName,
            CustomerName = order.Customer?.FullName,
            CustomerPhone = order.Customer?.Phone,
            CustomerAddress = order.Customer?.Address,
            GuestCount = order.GuestCount,
            OpenTime = order.OpenTime,
            CloseTime = order.CloseTime,
            ThankYouText = company.KassaReceiptThankYouText,
            FoodTotal = foodTotal,
            ServiceAmount = order.ServiceAmount,
            DiscountAmount = order.DiscountAmount,
            GrandTotal = grandTotal,
            DepositLimit = order.DepositAmount,
            IsPaid = order.IsClosed,
            PaidCash = order.PaidCash,
            PaidCard = order.PaidCard,
            CustomPaymentMethodName = order.CustomPaymentMethod?.NameAz,
            Items = activeDetails.Select(d => new KassaReceiptLineItem
            {
                Name = d.ProductName,
                Qty = d.Quantity,
                Price = d.Price,
                Total = d.TotalPrice,
                Note = d.ItemNote,
            }).ToList(),
        };
    }

    private static decimal PayableAmount(OrderHeader order)
    {
        var beh = order.BehAmount <= 0 ? 0m : order.BehAmount > order.TotalAmount ? order.TotalAmount : order.BehAmount;
        return Math.Max(0m, order.TotalAmount - beh);
    }
}
