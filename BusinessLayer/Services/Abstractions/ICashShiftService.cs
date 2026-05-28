using BusinessLayer.DTOs.CashShift;

namespace BusinessLayer.Services.Abstractions;

public interface ICashShiftService
{
    Task<CashShiftGetDto> GetActiveShiftAsync(Guid companyId);
    Task OpenShiftAsync(CashShiftOpenDto dto);
    Task CloseShiftAsync(CashShiftCloseDto dto);
    Task<object> GetShiftHistoryAsync(Guid companyId, int page = 1, int pageSize = 10);
    Task<object> GetActiveShiftOrdersAsync(Guid companyId, int page = 1, int pageSize = 10);
    Task<CashShiftGetDto?> RegenerateWaiterCodeAsync(Guid companyId);
}