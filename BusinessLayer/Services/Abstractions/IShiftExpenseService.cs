using BusinessLayer.DTOs.CashShift;

namespace BusinessLayer.Services.Abstractions;

public interface IShiftExpenseService
{
    Task<IReadOnlyList<ShiftExpenseGetDto>> ListActiveShiftAsync(Guid companyId);

    Task<ShiftExpenseGetDto> AddAsync(ShiftExpensePostDto dto, Guid userId, string username);

    Task DeleteAsync(Guid expenseId, Guid companyId, Guid userId, string deletedBy);

    Task<(IReadOnlyList<ShiftExpenseGetDto> Items, int TotalCount, decimal TotalAmount)> ListHistoryAsync(
        Guid companyId,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize,
        Guid? cashShiftId = null);
}
