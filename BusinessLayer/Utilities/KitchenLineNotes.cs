using BusinessLayer.DTOs.Kitchen;
using Domain.Entities;
using Domain.Enums;

namespace BusinessLayer.Utilities;

public static class KitchenLineNotes
{
    public static string TrimNote(string? s) => (s ?? "").Trim();

    /// <summary>Set tərkibi köhnə sətirlərdə ItemNote-da mötərizəli blok kimi saxlanılıb.</summary>
    public static bool ItemNoteLooksLikeSetComposition(string? user)
    {
        var u = TrimNote(user);
        return u.Contains('(') && u.Contains(')');
    }

    /// <summary>Mətbəx əməliyyatı / müqayisə üçün birləşmiş mətn (köhnə sətirlərdə yalnız ItemNote ola bilər).</summary>
    public static string CombinedForKitchen(OrderDetail detail)
    {
        var comp = TrimNote(detail.KitchenCompositionNote);
        var user = TrimNote(detail.ItemNote);
        if (!string.IsNullOrEmpty(comp)) return string.IsNullOrEmpty(user) ? comp : $"{comp}\n{user}";
        return user;
    }

    public static KitchenPrintItemDto ToPrintItem(
        KitchenOperation op,
        OrderDetail? detail,
        double sentQtyBeforeThisBatch,
        double lineTotalQty)
    {
        var status = op.OperationType switch
        {
            KitchenOperationType.New => "YENİ",
            KitchenOperationType.Reduced => "AZALDI",
            _ => "LƏĞV",
        };

        if (op.OperationType == KitchenOperationType.Cancelled)
        {
            return new KitchenPrintItemDto
            {
                Name = op.ProductName,
                Qty = Math.Abs(op.Quantity),
                Status = status,
                Note = "SİLİNDİ",
                CompositionNote = null,
                Total = lineTotalQty,
            };
        }

        var noteChanged = !string.IsNullOrWhiteSpace(op.Note);
        var isFirstSend = op.OperationType == KitchenOperationType.New && sentQtyBeforeThisBatch <= 1e-9;

        var comp = detail != null ? TrimNote(detail.KitchenCompositionNote) : "";
        var user = detail != null ? TrimNote(detail.ItemNote) : TrimNote(op.Note);

        string compositionNote = "";
        string userNote = "";

        if (string.IsNullOrEmpty(comp) && !string.IsNullOrEmpty(user))
        {
            // Set tərkibi köhnə formada ItemNote-da `(…)` ilə ola bilər; adi qeyd yalnız «Qeyd»
            if (ItemNoteLooksLikeSetComposition(user))
            {
                if (isFirstSend) compositionNote = user;
            }
            else
            {
                userNote = user;
            }
        }
        else if (!string.IsNullOrEmpty(comp))
        {
            if (isFirstSend) compositionNote = comp;
            if (!string.IsNullOrEmpty(user)) userNote = user;
        }
        else if (noteChanged)
        {
            userNote = TrimNote(op.Note);
        }

        return new KitchenPrintItemDto
        {
            Name = op.ProductName,
            Qty = Math.Abs(op.Quantity),
            Status = status,
            Note = userNote,
            CompositionNote = string.IsNullOrEmpty(compositionNote) ? null : compositionNote,
            Total = lineTotalQty,
        };
    }
}
