using BusinessLayer.DTOs.BossTelegram;
using BusinessLayer.Services.Abstractions;
using DAL.Server.Context;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;

namespace BusinessLayer.Services.Implementations;

public class BossTelegramChatService : IBossTelegramChatService
{
    private readonly AppDbContext _db;

    public BossTelegramChatService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<BossTelegramChatRowDto>> ListAsync(Guid companyId, CancellationToken ct = default)
    {
        return await _db.BossTelegramChats.AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .OrderByDescending(x => x.LinkedAt)
            .Select(x => new BossTelegramChatRowDto { ChatId = x.ChatId, LinkedAt = x.LinkedAt })
            .ToListAsync(ct);
    }

    public async Task LinkAsync(Guid companyId, Guid userId, long chatId, CancellationToken ct = default)
    {
        var existing = await _db.BossTelegramChats
            .FirstOrDefaultAsync(x => x.CompanyId == companyId && x.ChatId == chatId, ct);
        var now = DateTime.UtcNow;
        if (existing != null)
        {
            existing.UserId = userId;
            existing.LinkedAt = now;
            await _db.SaveChangesAsync(ct);
            return;
        }

        await _db.BossTelegramChats.AddAsync(new BossTelegramChat
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            UserId = userId,
            ChatId = chatId,
            LinkedAt = now,
        }, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UnlinkAsync(Guid companyId, long chatId, CancellationToken ct = default)
    {
        var row = await _db.BossTelegramChats
            .FirstOrDefaultAsync(x => x.CompanyId == companyId && x.ChatId == chatId, ct);
        if (row == null) return;
        _db.BossTelegramChats.Remove(row);
        await _db.SaveChangesAsync(ct);
    }

    public async Task SyncSubscriberChatIdsAsync(Guid companyId, Guid userId, IReadOnlyList<long> chatIds, CancellationToken ct = default)
    {
        var ids = chatIds.Where(x => x != 0).Distinct().ToHashSet();
        var existing = await _db.BossTelegramChats
            .Where(x => x.CompanyId == companyId)
            .ToListAsync(ct);

        foreach (var row in existing.Where(x => !ids.Contains(x.ChatId)))
            _db.BossTelegramChats.Remove(row);

        var now = DateTime.UtcNow;
        foreach (var chatId in ids)
        {
            var row = existing.FirstOrDefault(x => x.ChatId == chatId);
            if (row != null)
            {
                row.UserId = userId;
                row.LinkedAt = now;
                continue;
            }

            await _db.BossTelegramChats.AddAsync(new BossTelegramChat
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                UserId = userId,
                ChatId = chatId,
                LinkedAt = now,
            }, ct);
        }

        await _db.SaveChangesAsync(ct);
    }
}
