using BusinessLayer.DTOs.CashShift;
using BusinessLayer.Services.Abstractions;
using DAL.Server.Context;
using Domain.Common.Entities;
using Microsoft.EntityFrameworkCore;

namespace BusinessLayer.Services.Implementations;

public class ShiftExpenseService : IShiftExpenseService
{
    private readonly AppDbContext _context;

    public ShiftExpenseService(AppDbContext context)
    {
        _context = context;
    }

    private static ShiftExpenseGetDto Map(CashShiftExpense e, CashShift shift)
    {
        return new ShiftExpenseGetDto
        {
            Id = e.Id,
            CashShiftId = e.CashShiftId,
            Amount = e.Amount,
            Note = e.Note ?? "",
            CreatedAt = e.CreatedAt,
            CreatedBy = e.CreatedBy ?? "",
            RecordedByUserName = e.RecordedByUser?.FullName,
            ShiftStartTime = shift.StartTime,
            ShiftEndTime = shift.EndTime,
            ShiftIsClosed = shift.IsClosed,
        };
    }

    private async Task EnsureCashShiftPermissionAsync(Guid userId)
    {
        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user?.Role == null)
            throw new Exception("İstifadəçi tapılmadı.");

        var permissions = user.Role.Permissions ?? Array.Empty<int>();
        if (!user.Role.IsAdmin && !permissions.Any(p => p == 20))
            throw new Exception("Bu əməliyyat üçün kassa növbəsi icazəsi (20) və ya admin lazımdır.");
    }

    private async Task EnsureViewArchivePermissionAsync(Guid userId)
    {
        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user?.Role == null)
            throw new Exception("İstifadəçi tapılmadı.");

        var permissions = user.Role.Permissions ?? Array.Empty<int>();
        if (!user.Role.IsAdmin && !permissions.Any(p => p == 23))
            throw new Exception("Bu əməliyyat üçün «Arxivi görə bilər» icazəsi (23) və ya admin lazımdır.");
    }

    public async Task<IReadOnlyList<ShiftExpenseGetDto>> ListActiveShiftAsync(Guid companyId)
    {
        var shift = await _context.CashShifts
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.CompanyId == companyId && !s.IsClosed);

        if (shift == null)
            return Array.Empty<ShiftExpenseGetDto>();

        var items = await _context.CashShiftExpenses
            .AsNoTracking()
            .Include(e => e.RecordedByUser)
            .Where(e => e.CompanyId == companyId && e.CashShiftId == shift.Id && !e.IsDeleted)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();

        return items.Select(e => Map(e, shift)).ToList();
    }

    public async Task<ShiftExpenseGetDto> AddAsync(ShiftExpensePostDto dto, Guid userId, string username)
    {
        await EnsureCashShiftPermissionAsync(userId);

        if (dto.Amount <= 0)
            throw new Exception("Məbləğ sıfırdan böyük olmalıdır.");

        var note = (dto.Note ?? "").Trim();
        if (note.Length > 1000)
            throw new Exception("Qeyd çox uzundur (max 1000 simvol).");

        var shift = await _context.CashShifts
            .FirstOrDefaultAsync(s => s.CompanyId == dto.CompanyId && !s.IsClosed);

        if (shift == null)
            throw new Exception("Açıq növbə yoxdur — xərc əlavə etmək mümkün deyil.");

        var row = new CashShiftExpense
        {
            Id = Guid.NewGuid(),
            CompanyId = dto.CompanyId,
            CashShiftId = shift.Id,
            Amount = dto.Amount,
            Note = string.IsNullOrEmpty(note) ? "—" : note,
            RecordedByUserId = userId,
            CreatedBy = username,
        };

        await _context.CashShiftExpenses.AddAsync(row);
        await _context.SaveChangesAsync();

        await _context.Entry(row).Reference(e => e.RecordedByUser).LoadAsync();

        return Map(row, shift);
    }

    public async Task DeleteAsync(Guid expenseId, Guid companyId, Guid userId, string deletedBy)
    {
        await EnsureViewArchivePermissionAsync(userId);

        var row = await _context.CashShiftExpenses
            .Include(e => e.CashShift)
            .FirstOrDefaultAsync(e => e.Id == expenseId && e.CompanyId == companyId && !e.IsDeleted);

        if (row == null)
            throw new Exception("Xərc tapılmadı.");

        if (row.CashShift.IsClosed)
            throw new Exception("Bağlanmış növbənin xərclərini silmək olmaz.");

        row.IsDeleted = true;
        row.DeletedAt = DateTime.UtcNow.AddHours(4);
        row.DeletedBy = string.IsNullOrWhiteSpace(deletedBy) ? userId.ToString() : deletedBy.Trim();
        await _context.SaveChangesAsync();
    }

    public async Task<(IReadOnlyList<ShiftExpenseGetDto> Items, int TotalCount, decimal TotalAmount)> ListHistoryAsync(
        Guid companyId,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize,
        Guid? cashShiftId = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var q = _context.CashShiftExpenses
            .AsNoTracking()
            .Include(e => e.CashShift)
            .Include(e => e.RecordedByUser)
            .Where(e => e.CompanyId == companyId && !e.IsDeleted);

        if (cashShiftId.HasValue && cashShiftId.Value != Guid.Empty)
            q = q.Where(e => e.CashShiftId == cashShiftId.Value);
        else
        {
            if (from.HasValue)
                q = q.Where(e => e.CreatedAt.Date >= from.Value.Date);
            if (to.HasValue)
                q = q.Where(e => e.CreatedAt.Date <= to.Value.Date);
        }

        var total = await q.CountAsync();
        var totalAmount = await q.SumAsync(e => e.Amount);

        var items = await q
            .OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var list = items.Select(e => Map(e, e.CashShift)).ToList();
        return (list, total, totalAmount);
    }
}
