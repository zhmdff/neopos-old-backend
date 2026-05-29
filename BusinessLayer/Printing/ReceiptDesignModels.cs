using System.Text.Json.Serialization;

namespace BusinessLayer.Printing;

public sealed class ReceiptDesignRoot
{
    [JsonPropertyName("cashier")]
    public ReceiptDesignCashier? Cashier { get; set; }

    [JsonPropertyName("kitchen")]
    public ReceiptDesignKitchen? Kitchen { get; set; }
}

public sealed class ReceiptDesignCashier
{
    [JsonPropertyName("sections")]
    public List<ReceiptDesignSection>? Sections { get; set; }
}

public sealed class ReceiptDesignKitchen
{
    [JsonPropertyName("sections")]
    public List<ReceiptDesignSection>? Sections { get; set; }

    [JsonPropertyName("lan")]
    public ReceiptDesignKitchenLan? Lan { get; set; }
}

public sealed class ReceiptDesignKitchenLan
{
    [JsonPropertyName("escPosCompact")]
    public bool EscPosCompact { get; set; }
}

public sealed class ReceiptDesignSection
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = "";

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("size")]
    public string Size { get; set; } = "md";

    [JsonPropertyName("thickness")]
    public string Thickness { get; set; } = "normal";

    [JsonPropertyName("align")]
    public string Align { get; set; } = "left";
}
