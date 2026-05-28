using BusinessLayer.DTOs.Reports;

namespace BusinessLayer.Services.Abstractions;

public interface IReportService
{
    Task<SummaryReportDto> GetGeneralSummaryAsync(
        DateTime start,
        DateTime end,
        Guid companyId,
        bool includeOpenTables = false,
        DateTime? openOrdersOpenedOnOrAfter = null,
        Guid? cashShiftAttributionId = null);

    Task<BreakdownReportDto<TableBreakdownItemDto>> GetTableBreakdownAsync(
        DateTime start,
        DateTime end,
        Guid companyId,
        bool includeOpenTables = false,
        DateTime? openOrdersOpenedOnOrAfter = null,
        Guid? cashShiftAttributionId = null);

    Task<BreakdownReportDto<WaiterBreakdownItemDto>> GetWaiterBreakdownAsync(
        DateTime start,
        DateTime end,
        Guid companyId,
        bool includeOpenTables = false,
        DateTime? openOrdersOpenedOnOrAfter = null,
        Guid? cashShiftAttributionId = null);

    Task<BreakdownReportDto<WorkshopBreakdownItemDto>> GetWorkshopBreakdownAsync(
        DateTime start,
        DateTime end,
        Guid companyId,
        bool includeOpenTables = false,
        DateTime? openOrdersOpenedOnOrAfter = null,
        Guid? cashShiftAttributionId = null);

    Task<BreakdownReportDto<ProductBreakdownItemDto>> GetProductBreakdownAsync(
        DateTime start,
        DateTime end,
        Guid companyId,
        int take = 50,
        bool includeOpenTables = false,
        DateTime? openOrdersOpenedOnOrAfter = null,
        Guid? cashShiftAttributionId = null);

    Task<BreakdownReportDto<ProductBreakdownItemDto>> GetShiftProductBreakdownAsync(
        Guid shiftId,
        Guid companyId,
        int take = 50,
        bool includeOpenTables = false);

    Task<BreakdownReportDto<TableBreakdownItemDto>> GetShiftTableBreakdownAsync(Guid shiftId, Guid companyId, bool includeOpenTables = false);

    Task<BreakdownReportDto<WaiterBreakdownItemDto>> GetShiftWaiterBreakdownAsync(Guid shiftId, Guid companyId, bool includeOpenTables = false);

    Task<BreakdownReportDto<WorkshopBreakdownItemDto>> GetShiftWorkshopBreakdownAsync(
        Guid shiftId,
        Guid companyId,
        bool includeOpenTables = false);

    Task<CustomerLoyaltyReportDto> GetCustomerLoyaltyAsync(DateTime start, DateTime end, Guid companyId, string? q = null, int take = 200);

    Task<ShiftReportDto> GetShiftReportAsync(Guid shiftId, Guid companyId, bool includeOpenTables = false);

    Task<(List<ShiftReportDto> Items, int TotalCount)> GetAllShiftsAsync(int page, int pageSize, Guid companyId);
}