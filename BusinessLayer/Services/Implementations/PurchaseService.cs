using AutoMapper;
using BusinessLayer.DTOs.Purchase;
using BusinessLayer.Services.Abstractions;
using DAL.Server.Context;
using Domain.Common.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BusinessLayer.Services.Implementations;

public class PurchaseService : IPurchaseService
{
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;

    public PurchaseService(AppDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task CreateAsync(PurchasePostDto dto)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var purchase = new Purchase
            {
                CompanyId = dto.CompanyId,
                SupplierId = dto.SupplierId,
                WarehouseId = dto.WarehouseId,
                PurchaseDate = dto.PurchaseDate,
                InvoiceNumber = dto.InvoiceNumber,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "System",
                PurchaseItems = new List<PurchaseItem>()
            };

            decimal totalAmount = 0;

            foreach (var itemDto in dto.Items)
            {
                // 🔥 Hər bir sətir üçün Set və ya Adi məhsul məntiqini işlədirik
                await ProcessProductEntry(itemDto.ProductId, itemDto.Quantity, itemDto.PriceAtPurchase, itemDto.WarehouseId ?? dto.WarehouseId, dto, purchase);

                totalAmount += (itemDto.Quantity * itemDto.PriceAtPurchase);
            }

            purchase.TotalAmount = totalAmount;
            await _context.Purchases.AddAsync(purchase);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            throw new Exception("Xəta baş verdi: " + ex.Message);
        }
    }

    private async Task ProcessProductEntry(Guid productId, decimal quantity, decimal priceAtPurchase, Guid warehouseId, PurchasePostDto dto, Purchase purchase)
    {
        var product = await _context.Products.FindAsync(productId);
        if (product == null) throw new Exception($"Məhsul tapılmadı! ID: {productId}");

        var productSet = await _context.ProductSets
            .Include(ps => ps.SetItems)
            .FirstOrDefaultAsync(ps => ps.ProductId == productId && ps.CompanyId == dto.CompanyId);

        if (productSet != null && productSet.SetItems.Any())
        {
            var defaultSaleWarehouse = await _context.Warehouses
                .FirstOrDefaultAsync(w => w.CompanyId == dto.CompanyId && w.IsDefaultSale && !w.IsDeleted);

            foreach (var setItem in productSet.SetItems)
            {
                decimal calculatedQty = quantity * (decimal)setItem.Quantity;
                if (defaultSaleWarehouse != null)
                {
                    var ingredient = await _context.Products.FindAsync(setItem.ProductId);
                    if (ingredient != null)
                    {
                        decimal oldIngStock = ingredient.Stock;
                        ingredient.Stock -= calculatedQty;

                        await _context.ProductStockHistories.AddAsync(new ProductStockHistory
                        {
                            CompanyId = dto.CompanyId,
                            ProductId = setItem.ProductId,
                            WarehouseId = defaultSaleWarehouse.Id,
                            QuantityBefore = oldIngStock,
                            ChangeAmount = -calculatedQty,
                            QuantityAfter = ingredient.Stock,
                            MovementType = StockMovementType.Transfer,
                            Note = $"Set Mədaxili Sərfiyyatı (Ana Set: {product.NameAz})",
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = "System"
                        });
                    }
                }
            }

            decimal oldSetStock = product.Stock;
            product.Stock += quantity;

            purchase.PurchaseItems.Add(new PurchaseItem
            {
                CompanyId = dto.CompanyId,
                ProductId = productId,
                Quantity = quantity,
                // 🔥 BU SƏTİR YOX İDİ: Qiyməti bura yazırıq
                PriceAtPurchase = priceAtPurchase,
                WarehouseId = warehouseId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "System"
            });

            await _context.ProductStockHistories.AddAsync(new ProductStockHistory
            {
                CompanyId = dto.CompanyId,
                ProductId = productId,
                WarehouseId = warehouseId,
                QuantityBefore = oldSetStock,
                ChangeAmount = quantity,
                QuantityAfter = product.Stock,
                MovementType = StockMovementType.Purchase,
                Note = $"Mədaxil (Hazır Set: {dto.InvoiceNumber})",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "System"
            });
        }
        else
        {
            decimal oldStock = product.Stock;
            product.Stock += quantity;

            purchase.PurchaseItems.Add(new PurchaseItem
            {
                CompanyId = dto.CompanyId,
                ProductId = productId,
                Quantity = quantity,
                // 🔥 BU SƏTİR YOX İDİ: Qiyməti bura yazırıq
                PriceAtPurchase = priceAtPurchase,
                WarehouseId = warehouseId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "System"
            });

            await _context.ProductStockHistories.AddAsync(new ProductStockHistory
            {
                CompanyId = dto.CompanyId,
                ProductId = productId,
                WarehouseId = warehouseId,
                SupplierId = dto.SupplierId,
                QuantityBefore = oldStock,
                ChangeAmount = quantity,
                QuantityAfter = product.Stock,
                MovementType = StockMovementType.Purchase,
                Note = $"Mədaxil (Qaimə: {dto.InvoiceNumber})",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "System"
            });
        }
    }

        public async Task<(IEnumerable<PurchaseGetDto> items, int totalCount)> GetAllByCompanyIdAsync(Guid companyId, int pageNumber, int pageSize)
    {
        var query = _context.Purchases
            .Include(p => p.Supplier)
            .Include(p => p.Warehouse)
            .Include(p => p.PurchaseItems).ThenInclude(pi => pi.Product)
            .Include(p => p.PurchaseItems).ThenInclude(pi => pi.Warehouse)
            .Where(x => x.CompanyId == companyId && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt);

        int totalCount = await query.CountAsync();
        var purchases = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();

        return (_mapper.Map<IEnumerable<PurchaseGetDto>>(purchases), totalCount);
    }

    public async Task<PurchaseGetDto> GetByIdAsync(Guid id)
    {
        var purchase = await _context.Purchases
            .Include(p => p.Supplier)
            .Include(p => p.Warehouse)
            .Include(p => p.PurchaseItems).ThenInclude(pi => pi.Product)
            .Include(p => p.PurchaseItems).ThenInclude(pi => pi.Warehouse)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

        if (purchase == null)
            throw new Exception($"Qaimə tapılmadı!");

        return _mapper.Map<PurchaseGetDto>(purchase);
    }
}