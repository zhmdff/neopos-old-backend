using Domain.Enums;

namespace Domain.Common.Entities;

public class Company : AuditableEntity
{
    public string? Logo { get; set; }

    public string NameAz { get; set; }
    public string NameRu { get; set; }
    public string NameEn { get; set; }

    public string AddressAz { get; set; }
    public string AddressRu { get; set; }
    public string AddressEn { get; set; }

    public string PhoneNumber1 { get; set; }
    public string? PhoneNumber2 { get; set; }
    public string? PhoneNumber3 { get; set; }

    public string Slug { get; set; }

    /// <summary>
    /// Unique key used for initial bootstrap and synchronization identification.
    /// </summary>
    public string? TenantKey { get; set; }

    public DateTime PackageEndDate { get; set; }
    public bool IsActive { get; set; } = true;

    public bool IsDeliveryPriceEnabled { get; set; } = false;

    public bool IsUserModeActive {  get; set; } = false;

    /// <summary>
    /// Terminal: masa açılan kimi qonaq sayını soruş (istəyə bağlı).
    /// </summary>
    public bool IsGuestModeActive { get; set; } = false;

    /// <summary>
    /// Terminalda masalar üçün ilkin düzülüş: şəbəkə və ya xəritə.
    /// </summary>
    public TablesLayoutMode TablesLayoutMode { get; set; } = TablesLayoutMode.Normal;

    /// <summary>eKassam (OneClick) — hər restoran üçün ayrıca.</summary>
    public bool EkassamEnabled { get; set; }

    /// <summary>Məs: http://192.168.1.10:8080 (sonda / olmasın).</summary>
    public string? EkassamBaseUrl { get; set; }

    /// <summary>Token hesabı üçün gizli açar (API sənədləşməsində «key»).</summary>
    public string? EkassamApiKey { get; set; }

    /// <summary>Terminalda Bakı vaxtı ilə avtomatik kassa növbəsi (serverdə saxlanır).</summary>
    public bool AutoCashShiftEnabled { get; set; }

    /// <summary>HH:mm (Bakı).</summary>
    public string AutoCashShiftOpenTime { get; set; } = "09:00";

    /// <summary>HH:mm (Bakı). Gecə növbəsi: açılışdan «kiçik» vaxt (məs. 09:00 aç, 06:00 bağ).</summary>
    public string AutoCashShiftCloseTime { get; set; } = "23:00";

    public bool AutoCashShiftForceClose { get; set; } = true;

    /// <summary>Növbə əl ilə açılanda terminalda növbə depoziti (kassa) soruşulsun.</summary>
    public bool CashShiftPromptOpeningDeposit { get; set; }

    /// <summary>Növbə bağlananda cari növbənin sadə terminal hesabatı avtomatik çap olunsun.</summary>
    public bool CashShiftPrintReportOnClose { get; set; } = true;

    /// <summary>Boss/Terminal: kassa printer hədəfi (ad / IP / digər formatlar).</summary>
    public string? CashierPrinterTarget { get; set; }

    /// <summary>Boss/Terminal: mətbəx printer hədəfi (default).</summary>
    public string? KitchenPrinterTarget { get; set; }

    /// <summary>
    /// Çek dizaynı üçün JSON (kassa + mətbəx ölçüləri). Şirkət üzrə qalıcı saxlanılır.
    /// </summary>
    public string? ReceiptDesignSettingsJson { get; set; }

    /// <summary>Terminal kassa çekinin sonunda (təşəkkür sətiri). Boşdursa varsayılan mətn.</summary>
    public string? KassaReceiptThankYouText { get; set; }

    /// <summary>POS giriş / kilid ekranı üçün fon şəkli (wwwroot path).</summary>
    public string? PosLockScreenImage { get; set; }

    /// <summary>Müştəri ekranı (ikinci monitor və s.) üçün kilid fon şəkli.</summary>
    public string? CustomerDisplayLockScreenImage { get; set; }

    /// <summary>
    /// Terminal menyu: kateqoriyaların üstündə şöbə seçimi; məhsullar seçilmiş şöbəyə görə süzülür.
    /// Default false — mövcud düzülüş saxlanılır.
    /// </summary>
    public bool MenuFilterByWorkshop { get; set; }

    /// <summary>
    /// Terminal bildiriş seçimi: mətbəxə göndərilmiş sətir silinəndə Telegram/Boss təsdiqi.
    /// Serverdə saxlanılır ki, admin paneldə «Silinmə təsdiqləri» yalnız aktiv olanda görünsün.
    /// </summary>
    public bool TerminalLineDeleteConfirmEnabled { get; set; }

    /// <summary>
    /// Terminal (Electron) «Bildirişlər» bölməsində saxlanan Telegram bot token-i — ofisiant/brauzer
    /// silinmə təsdiqi mesajı üçün serverdə istifadə olunur (appsettings tokenundan sonra fallback).
    /// </summary>
    public string? TelegramBotToken { get; set; }

    /// <summary>Terminal «Bildiriş seçimləri» JSON — server audit Telegram bildirişləri üçün.</summary>
    public string? TelegramNotifyPrefsJson { get; set; }
}
