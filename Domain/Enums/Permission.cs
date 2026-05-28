using System.ComponentModel.DataAnnotations;

namespace Domain.Enums;

public enum Permission
{
    [Display(Name = "Çek yarada bilər")] CreateCheck = 1,
    [Display(Name = "Boş çeki silə bilər")] DeleteEmptyCheck = 2,
    [Display(Name = "Extra əlavə edə bilər")] AddExtra = 3,
    [Display(Name = "Depozit tətbiq edə bilər")] ApplyDeposit = 4,
    [Display(Name = "Preçek çıxarda bilər")] PrintPrecheck = 5,
    [Display(Name = "Endirim tətbiq edə bilər")] ApplyDiscount = 6,
    [Display(Name = "Ofisiantı dəyişə bilər")] ChangeWaiter = 7,
    [Display(Name = "Xidmət haqqını dəyişə bilər")] ChangeServiceCharge = 8,
    [Display(Name = "Çekdən məhsul silə bilər")] RemoveProductFromCheck = 9,
    [Display(Name = "Çekləri birləşdirə bilər")] MergeChecks = 10,
    [Display(Name = "Məhsul transfer edə bilər")] TransferProduct = 11,
    [Display(Name = "Qonaq sayını dəyişə bilər")] ChangeGuestCount = 12,
    [Display(Name = "Çeki bağlıya bilər")] CloseCheck = 13,
    [Display(Name = "Çekə şərh yaza bilmək")] AddCheckComment = 14,
    [Display(Name = "Müştəri seçə bilər")] SelectCustomer = 15,

    [Display(Name = "Masa dəyişə bilər")] ChangeTable = 16,
    [Display(Name = "Terminaldan çıxış edə bilər")] ExitTerminal = 17,
    [Display(Name = "Printeri sazlıya bilər")] SetupPrinter = 18,
    [Display(Name = "Vergi inteqrasiya")] TaxIntegration = 19,
    [Display(Name = "Kassa növbəsi başlada bilər")] StartCashShift = 20,

    [Display(Name = "Hesabatı görə bilər")] ViewReports = 21,
    [Display(Name = "Başqalarının çeklərini görə bilər")] ViewOthersChecks = 22,
    [Display(Name = "Arxivi görə bilər")] ViewArchive = 23,
    [Display(Name = "Sifarişdə məhsul adını və qiymətini dəyişə bilər")] EditOrderLineNamePrice = 24,

    [Display(Name = "Mətbəxə göndərildikdən sonra məhsul silə bilər")]
    RemoveProductAfterKitchenSent = 25,

    [Display(Name = "Silinmə/miqdar azaltma Telegram təsdiqi ilə (hər addım)")]
    RequireTelegramConfirmForRemoveOrQtyDecrease = 26
}
