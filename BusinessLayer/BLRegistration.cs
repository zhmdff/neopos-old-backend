using Application.Interfaces;
using BusinessLayer.Concrete;
using BusinessLayer.ExternalServices.Abstractions;
using BusinessLayer.ExternalServices.Implementations;
using BusinessLayer.Services.Abstractions;
using BusinessLayer.Services.Implementations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace BusinessLayer;

public static class BLRegistration
{
    public static void AddBlServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAutoMapper(Assembly.GetExecutingAssembly());

        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<ITranslationService, PythonTranslationService>();
        services.AddScoped<ICompanyService, CompanyService>();
        services.AddScoped<ICompanyPaymentMethodService, CompanyPaymentMethodService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ITenantBootstrapService, TenantBootstrapService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IHallService, HallService>();
        services.AddScoped<IHallTimeDiscountRuleService, HallTimeDiscountRuleService>();
        services.AddScoped<ITableService, TableService>();
        services.AddScoped<IWorkshopService, WorkshopService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IProductVariantService, ProductVariantService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IMenuImportService, MenuImportService>();
        services.AddScoped<ICashShiftService, CashShiftService>();
        services.AddScoped<IShiftExpenseService, ShiftExpenseService>();
        services.AddScoped<IProductSetService, ProductSetService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IQRMenuService, QRMenuService>();
        services.AddScoped<IKitchenService, KitchenService>();
        services.AddScoped<IWarehouseService, WarehouseService>();
        services.AddScoped<ISupplierService, SupplierService>();
        services.AddScoped<IPurchaseService, PurchaseService>();
        services.AddScoped<IStockHistoryService, StockHistoryService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IBossWebPushService, BossWebPushService>();
        services.AddScoped<IBossTelegramChatService, BossTelegramChatService>();
        services.AddScoped<IBossTelegramNotifyService, BossTelegramNotifyService>();
        services.AddScoped<IPendingLineDeleteConfirmService, PendingLineDeleteConfirmService>();
    }
}