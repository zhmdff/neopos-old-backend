namespace BusinessLayer.DTOs.OrderHeader;

/// <summary>Terminal «Masa tarixçəsi» — çek açılışı + audit hadisələri (xronoloji).</summary>
public class OrderJournalEntryDto
{
    public DateTime At { get; set; }
    /// <summary>open | audit</summary>
    public string Kind { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Detail { get; set; }
    public string? UserName { get; set; }
}
