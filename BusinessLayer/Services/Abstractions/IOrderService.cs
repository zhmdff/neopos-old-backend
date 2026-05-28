using BusinessLayer.DTOs.Kitchen;
using BusinessLayer.DTOs.OrderDetail;
using BusinessLayer.DTOs.OrderHeader;

namespace BusinessLayer.Services.Abstractions;

public interface IOrderService
{
    Task<OrderHeaderGetDto> OpenOrderAsync(OrderHeaderPostDto dto);
    Task<OrderHeaderGetDto> GetActiveOrderContentsAsync(Guid tableId, Guid companyId);
    Task<OrderHeaderGetDto> AddItemsToOrderAsync(Guid orderId, List<OrderDetailPostDto> items, Guid companyId);
    Task<OrderHeaderGetDto> UpdateOrderItemAsync(Guid detailId, OrderDetailUpdateDto dto);
    Task<OrderHeaderGetDto> RemoveOrderItemAsync(Guid detailId, Guid companyId, string? reason = null);
    Task<bool> DeleteOrderAsync(Guid orderId, Guid companyId);
    Task<OrderHeaderGetDto> UpdateServiceFeeAsync(Guid orderId, decimal newPercentage, Guid companyId);
    Task<OrderHeaderGetDto?> UpdateOrderDepositAsync(Guid orderId, decimal amount, TimeSpan? start, TimeSpan? end, Guid companyId); // Cursor yeniləmə etdi
    Task<OrderHeaderGetDto> UpdateOrderDiscountAsync(Guid orderId, decimal value, bool isPercentage, Guid companyId);
    Task<OrderHeaderGetDto> UpdateOrderBehAsync(Guid orderId, decimal amount, Guid companyId);
    Task<OrderHeaderGetDto> UpdateOrderNoteAsync(Guid orderId, string? note, Guid companyId);
    Task<OrderHeaderGetDto> UpdateOrderGuestCountAsync(Guid orderId, int? guestCount, Guid companyId);
    Task<OrderHeaderGetDto> UpdateTableHourBonusAsync(Guid orderId, int bonusMinutes, Guid companyId);
    Task<OrderHeaderGetDto> ChangeOrderWaiterAsync(Guid orderId, string fullName, Guid companyId);
    Task<bool> MarkOrderItemsAsSentAsync(MarkAsSentDto dto, Guid companyId);
    Task<bool> CloseOrderAsync(OrderCloseDto dto, Guid companyId);
    Task<object> GetClosedOrdersAsync(
        Guid companyId,
        DateTime? date = null,
        Guid? cashShiftId = null,
        int page = 1,
        int pageSize = 10,
        DateTime? startDate = null,
        DateTime? endDate = null);
    Task TransferTableAsync(Guid orderId, Guid targetTableId, Guid companyId);
    Task<(Guid SourceOrderId, Guid TargetOrderId)> TransferOrderItemAsync(Guid sourceDetailId, Guid targetTableId, double quantity, Guid companyId);
    Task<OrderHeaderGetDto> MergeOrdersAsync(Guid targetOrderId, Guid sourceOrderId, Guid companyId);
    Task<OrderHeaderGetDto> UpdateOrderSplitAssignmentsAsync(Guid orderId, UpdateOrderSplitsDto dto, Guid companyId);
    /// <summary>Parça üzrə ödəniş; bütün məbləğ toplananda sifariş bağlanır. Bağlanıbsa null qaytarır.</summary>
    Task<OrderHeaderGetDto?> PayOrderSplitAsync(PaySplitDto dto, Guid companyId);
    Task<List<string>> GetRecentItemNotesAsync(Guid companyId, int take = 10);
    Task<OrderHeaderGetDto> LinkOrderCustomerAsync(Guid orderId, Guid? customerId, Guid companyId);
    /// <summary>Növbə tarixçəsindən bağlı çeki yenidən aktivləşdirir (yalnız «Arxivi görə bilər» və ya admin).</summary>
    Task<OrderHeaderGetDto> ReopenShiftArchiveOrderAsync(
        Guid orderId,
        Guid companyId,
        Guid userId,
        string? presetKey,
        string? note);

    Task<List<OrderJournalEntryDto>> GetOrderJournalAsync(Guid orderId, Guid companyId);
}