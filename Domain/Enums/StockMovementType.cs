namespace Domain.Enums;

public enum StockMovementType
{
    Purchase = 1,      // Tədarük (Artım)
    Sale = 2,          // Satış (Azalma)
    Return = 3,        // Geri qaytarma (Artım)
    Waste = 4,         // Zay olmaq/Xarab olma (Azalma)
    Correction = 5,    // Əllə düzəliş (Hər iki hal ola bilər)
    Transfer = 6       // Anbarlararası transfer
}