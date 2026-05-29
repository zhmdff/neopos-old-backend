using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace Domain.Common.Entities;

public class QRMenuSetting : AuditableCompanyEntity
{
    public string? WifiName { get; set; }
    public string? WifiPassword { get; set; }

    public string? InstagramUrl { get; set; }
    public string? TiktokUrl { get; set; }
    public string? FacebookUrl { get; set; }

    public bool Phone1HasWhatsApp { get; set; }
    public bool Phone2HasWhatsApp { get; set; }
    public bool Phone3HasWhatsApp { get; set; }

    public string? WorkingHours { get; set; }
    public string? MapLocationUrl { get; set; }

    public decimal ServiceChargePercent { get; set; } = 0;

    [NotMapped]
    public List<string> GalleryImages
    {
        get => string.IsNullOrEmpty(GalleryImagesJson) 
            ? new() 
            : JsonSerializer.Deserialize<List<string>>(GalleryImagesJson, (JsonSerializerOptions?)null) ?? new();
        set => GalleryImagesJson = JsonSerializer.Serialize(value, (JsonSerializerOptions?)null);
    }

    public string GalleryImagesJson { get; set; } = "[]";
}