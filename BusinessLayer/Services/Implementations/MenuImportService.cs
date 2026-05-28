using BusinessLayer.DTOs.Category;
using BusinessLayer.DTOs.MenuImport;
using BusinessLayer.DTOs.Product;
using BusinessLayer.Services.Abstractions;
using ClosedXML.Excel;
using DAL.Server.Context;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BusinessLayer.Services.Implementations;

public class MenuImportService : IMenuImportService
{
    private readonly AppDbContext _context;
    private readonly ICategoryService _categoryService;
    private readonly IProductService _productService;

    public MenuImportService(AppDbContext context, ICategoryService categoryService, IProductService productService)
    {
        _context = context;
        _categoryService = categoryService;
        _productService = productService;
    }

    public byte[] GetTemplateWorkbook()
    {
        using var wb = new XLWorkbook();
        var wsCat = wb.Worksheets.Add("Kateqoriyalar");
        wsCat.Cell(1, 1).Value = "Kateqoriya";
        wsCat.Cell(1, 2).Value = "ValideynKateqoriya";
        wsCat.Cell(1, 3).Value = "Sıra";
        wsCat.Row(1).Style.Font.Bold = true;

        var wsPr = wb.Worksheets.Add("Məhsullar");
        wsPr.Cell(1, 1).Value = "MəhsulAdı";
        wsPr.Cell(1, 2).Value = "Kateqoriya";
        wsPr.Cell(1, 3).Value = "Emalatxana";
        wsPr.Cell(1, 4).Value = "Maya";
        wsPr.Cell(1, 5).Value = "SatışQiyməti";
        wsPr.Cell(1, 6).Value = "Barkod";
        wsPr.Cell(1, 7).Value = "Vahid";
        wsPr.Row(1).Style.Font.Bold = true;

        wsCat.Columns().AdjustToContents();
        wsPr.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public async Task<byte[]> ExportMenuWorkbookAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        if (!await _context.Companies.AnyAsync(c => c.Id == companyId, cancellationToken))
            throw new InvalidOperationException("Şirkət tapılmadı.");

        var categories = await (
            from c in _context.Categories.AsNoTracking()
            where c.CompanyId == companyId && !c.IsDeleted
            join p in _context.Categories.AsNoTracking() on c.ParentCategoryId equals p.Id into parents
            from p in parents.DefaultIfEmpty()
            orderby c.OrderIndex, c.NameAz
            select new { c.NameAz, c.OrderIndex, ParentName = p != null ? p.NameAz : null }
        ).ToListAsync(cancellationToken);

        var products = await _context.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Workshop)
            .Where(p => p.CompanyId == companyId && !p.IsDeleted)
            .OrderBy(p => p.Category!.NameAz)
            .ThenBy(p => p.OrderIndex)
            .ThenBy(p => p.NameAz)
            .ToListAsync(cancellationToken);

        using var wb = new XLWorkbook();
        var wsCat = wb.Worksheets.Add("Kateqoriyalar");
        wsCat.Cell(1, 1).Value = "Kateqoriya";
        wsCat.Cell(1, 2).Value = "ValideynKateqoriya";
        wsCat.Cell(1, 3).Value = "Sıra";
        wsCat.Row(1).Style.Font.Bold = true;

        for (var i = 0; i < categories.Count; i++)
        {
            var r = i + 2;
            wsCat.Cell(r, 1).Value = categories[i].NameAz;
            wsCat.Cell(r, 2).Value = categories[i].ParentName ?? "";
            wsCat.Cell(r, 3).Value = categories[i].OrderIndex;
        }

        var wsPr = wb.Worksheets.Add("Məhsullar");
        wsPr.Cell(1, 1).Value = "MəhsulAdı";
        wsPr.Cell(1, 2).Value = "Kateqoriya";
        wsPr.Cell(1, 3).Value = "Emalatxana";
        wsPr.Cell(1, 4).Value = "Maya";
        wsPr.Cell(1, 5).Value = "SatışQiyməti";
        wsPr.Cell(1, 6).Value = "Barkod";
        wsPr.Cell(1, 7).Value = "Vahid";
        wsPr.Row(1).Style.Font.Bold = true;

        for (var i = 0; i < products.Count; i++)
        {
            var p = products[i];
            var r = i + 2;
            wsPr.Cell(r, 1).Value = p.NameAz;
            wsPr.Cell(r, 2).Value = p.Category?.NameAz ?? "";
            wsPr.Cell(r, 3).Value = p.Workshop?.NameAz ?? "";
            wsPr.Cell(r, 4).Value = p.CostPrice;
            wsPr.Cell(r, 5).Value = p.SalePrice;
            wsPr.Cell(r, 6).Value = p.Barcode ?? "";
            wsPr.Cell(r, 7).Value = UnitLabelAz(p.Unit);
        }

        wsCat.Columns().AdjustToContents();
        wsPr.Columns().AdjustToContents();

        using var outMs = new MemoryStream();
        wb.SaveAs(outMs);
        return outMs.ToArray();
    }

    private static string UnitLabelAz(SalesUnit u) => u switch
    {
        SalesUnit.Kg => "Kq",
        SalesUnit.Gram => "Qram",
        SalesUnit.Litre => "Litr",
        SalesUnit.Millilitre => "Ml",
        _ => "Ədəd"
    };

    public async Task<MenuImportPreviewResultDto> PreviewAsync(Stream excelStream, Guid companyId, CancellationToken cancellationToken = default)
    {
        var bytes = await CopyToBytesAsync(excelStream, cancellationToken);
        return await PreviewFromBytesAsync(bytes, companyId, cancellationToken);
    }

    public async Task<MenuImportApplyResultDto> ApplyAsync(Stream excelStream, Guid companyId, CancellationToken cancellationToken = default)
    {
        var bytes = await CopyToBytesAsync(excelStream, cancellationToken);

        var preview = await PreviewFromBytesAsync(bytes, companyId, cancellationToken);
        if (preview.GeneralErrors.Count > 0 || !preview.IsValid)
        {
            var parts = preview.GeneralErrors
                .Concat(preview.Products.Select(p => p.Error).Where(e => !string.IsNullOrEmpty(e))!)
                .ToList();
            throw new InvalidOperationException("Import mümkün deyil: " + string.Join(" ", parts));
        }

        using var wb = new XLWorkbook(new MemoryStream(bytes));
        var parsed = ParseWorkbook(wb);
        if (parsed == null)
            throw new InvalidOperationException("Excel oxuna bilmədi.");

        await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var existingCategories = await _context.Categories
                .Where(c => c.CompanyId == companyId && !c.IsDeleted)
                .ToListAsync(cancellationToken);

            var nameToId = existingCategories
                .GroupBy(c => NormalizeName(c.NameAz))
                .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

            var existingNameSet = nameToId.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var toCreateRows = parsed.CategoryRows
                .Where(r => !string.IsNullOrWhiteSpace(r.Name))
                .GroupBy(r => NormalizeName(r.Name))
                .Select(g => g.Last())
                .Where(r => !existingNameSet.Contains(NormalizeName(r.Name)))
                .ToList();

            var orderedCategories = OrderCategoriesForInsert(toCreateRows, nameToId);
            var catCreated = 0;
            foreach (var row in orderedCategories)
            {
                Guid? parentId = null;
                if (!string.IsNullOrWhiteSpace(row.ParentName))
                {
                    var pk = NormalizeName(row.ParentName);
                    if (!nameToId.TryGetValue(pk, out var pid))
                        throw new InvalidOperationException($"Valideyn kateqoriya tapılmadı: {row.ParentName}");
                    parentId = pid;
                }

                var post = new CategoryPostDto
                {
                    NameAz = row.Name.Trim(),
                    CompanyId = companyId,
                    ParentCategoryId = parentId,
                    OrderIndex = row.OrderIndex ?? 0
                };
                var id = await _categoryService.CreateAsync(post);
                nameToId[NormalizeName(row.Name)] = id;
                catCreated++;
            }

            var workshops = await _context.Workshops
                .Where(w => w.CompanyId == companyId && !w.IsDeleted)
                .ToListAsync(cancellationToken);
            var workshopByName = workshops
                .GroupBy(w => NormalizeName(w.NameAz))
                .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

            var prodCreated = 0;
            foreach (var pr in parsed.ProductRows)
            {
                if (string.IsNullOrWhiteSpace(pr.NameAz))
                    continue;

                if (!pr.SalePrice.HasValue || pr.SalePrice.Value < 0)
                    continue;

                var sale = pr.SalePrice.Value;
                var cost = pr.CostPrice ?? sale;
                if (pr.CostPrice.HasValue && sale < pr.CostPrice.Value)
                    continue;

                if (string.IsNullOrWhiteSpace(pr.CategoryName) || string.IsNullOrWhiteSpace(pr.WorkshopName))
                    continue;

                if (!nameToId.TryGetValue(NormalizeName(pr.CategoryName), out var categoryId))
                    continue;
                if (!workshopByName.TryGetValue(NormalizeName(pr.WorkshopName), out var workshopId))
                    continue;

                var unit = MapUnit(pr.UnitLabel);
                var post = new ProductPostDto
                {
                    NameAz = pr.NameAz.Trim(),
                    CompanyId = companyId,
                    CategoryId = categoryId,
                    WorkshopId = workshopId,
                    CostPrice = cost,
                    MarkupType = MarkupType.Amount,
                    MarkupValue = sale - cost,
                    Barcode = string.IsNullOrWhiteSpace(pr.Barcode) ? null : pr.Barcode.Trim(),
                    Unit = unit,
                    CookingProcess = null,
                    DeliveryPrice = null
                };

                _ = await _productService.CreateAsync(post);
                prodCreated++;
            }

            await tx.CommitAsync(cancellationToken);
            return new MenuImportApplyResultDto
            {
                CategoriesCreated = catCreated,
                ProductsCreated = prodCreated,
                Message = $"Kateqoriya: {catCreated}, məhsul: {prodCreated} yaradıldı."
            };
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static async Task<byte[]> CopyToBytesAsync(Stream excelStream, CancellationToken cancellationToken)
    {
        await using var ms = new MemoryStream();
        await excelStream.CopyToAsync(ms, cancellationToken);
        return ms.ToArray();
    }

    private async Task<MenuImportPreviewResultDto> PreviewFromBytesAsync(byte[] bytes, Guid companyId, CancellationToken cancellationToken)
    {
        using var ms = new MemoryStream(bytes);
        return await PreviewFromMemoryAsync(ms, companyId, cancellationToken);
    }

    private async Task<MenuImportPreviewResultDto> PreviewFromMemoryAsync(MemoryStream ms, Guid companyId, CancellationToken cancellationToken)
    {
        var result = new MenuImportPreviewResultDto();
        if (!await _context.Companies.AnyAsync(c => c.Id == companyId, cancellationToken))
        {
            result.GeneralErrors.Add("Şirkət tapılmadı.");
            return result;
        }

        ParsedWorkbook? parsed;
        try
        {
            using var wb = new XLWorkbook(ms);
            parsed = ParseWorkbook(wb);
        }
        catch (Exception ex)
        {
            result.GeneralErrors.Add($"Excel oxuna bilmədi: {ex.Message}");
            return result;
        }

        if (parsed == null || parsed.GeneralErrors.Count > 0)
        {
            result.GeneralErrors.AddRange(parsed?.GeneralErrors ?? ["Fayl boşdur və ya struktur səhvdir."]);
            return result;
        }

        var existingCategories = await _context.Categories
            .AsNoTracking()
            .Where(c => c.CompanyId == companyId && !c.IsDeleted)
            .ToListAsync(cancellationToken);

        var workshops = await _context.Workshops
            .AsNoTracking()
            .Where(w => w.CompanyId == companyId && !w.IsDeleted)
            .ToListAsync(cancellationToken);

        var nameToId = existingCategories
            .GroupBy(c => NormalizeName(c.NameAz))
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

        var workshopByName = workshops
            .GroupBy(w => NormalizeName(w.NameAz))
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

        var categoryPreview = BuildCategoryPreview(parsed.CategoryRows, nameToId);
        result.Categories = categoryPreview;

        var newCategoryNames = categoryPreview
            .Where(c => c.WillBeCreated)
            .Select(c => NormalizeName(c.NameAz))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!TryOrderNewCategories(parsed.CategoryRows, existingCategories.Select(c => NormalizeName(c.NameAz)).ToHashSet(StringComparer.OrdinalIgnoreCase), out var orderError))
            result.GeneralErrors.Add(orderError);

        foreach (var pr in parsed.ProductRows)
        {
            var dto = new MenuImportProductPreviewDto
            {
                ExcelRowNumber = pr.RowNumber,
                NameAz = pr.NameAz,
                CategoryName = pr.CategoryName,
                WorkshopName = pr.WorkshopName,
                Barcode = string.IsNullOrWhiteSpace(pr.Barcode) ? null : pr.Barcode.Trim(),
                UnitLabel = pr.UnitLabel ?? "Ədəd"
            };

            if (string.IsNullOrWhiteSpace(pr.NameAz))
            {
                dto.Error = "Məhsul adı boşdur.";
                result.Products.Add(dto);
                continue;
            }

            if (string.IsNullOrWhiteSpace(pr.CategoryName))
            {
                dto.Error = "Kateqoriya boşdur.";
                result.Products.Add(dto);
                continue;
            }

            if (string.IsNullOrWhiteSpace(pr.WorkshopName))
            {
                dto.Error = "Emalatxana boşdur.";
                result.Products.Add(dto);
                continue;
            }

            var catKey = NormalizeName(pr.CategoryName);
            if (!nameToId.ContainsKey(catKey) && !newCategoryNames.Contains(catKey))
            {
                dto.Error = $"Kateqoriya tapılmır (nə bazada, nə də Kateqoriyalar vərəqində): {pr.CategoryName}";
                result.Products.Add(dto);
                continue;
            }

            if (!workshopByName.ContainsKey(NormalizeName(pr.WorkshopName)))
            {
                dto.Error = $"Emalatxana tapılmadı (əvvəlcə sistemdə yaradın): {pr.WorkshopName}";
                result.Products.Add(dto);
                continue;
            }

            if (!pr.SalePrice.HasValue || pr.SalePrice.Value < 0)
            {
                dto.Error = "Satış qiyməti düzgün deyil (müsbət rəqəm olmalıdır).";
                result.Products.Add(dto);
                continue;
            }

            var sale = pr.SalePrice.Value;
            decimal cost;
            if (pr.CostPrice.HasValue)
            {
                cost = pr.CostPrice.Value;
                if (cost < 0)
                {
                    dto.Error = "Maya mənfi ola bilməz.";
                    result.Products.Add(dto);
                    continue;
                }

                if (sale < cost)
                {
                    dto.Error = "Satış qiyməti mayadan kiçik ola bilməz.";
                    result.Products.Add(dto);
                    continue;
                }
            }
            else
            {
                cost = sale;
            }

            dto.CostPrice = cost;
            dto.SalePrice = sale;
            result.Products.Add(dto);
        }

        return result;
    }

    private sealed class ParsedWorkbook
    {
        public List<string> GeneralErrors { get; } = [];
        public List<CategorySheetRow> CategoryRows { get; } = [];
        public List<ProductSheetRow> ProductRows { get; } = [];
    }

    private sealed class CategorySheetRow
    {
        public string Name { get; set; } = "";
        public string? ParentName { get; set; }
        public int? OrderIndex { get; set; }
    }

    private sealed class ProductSheetRow
    {
        public int RowNumber { get; set; }
        public string NameAz { get; set; } = "";
        public string CategoryName { get; set; } = "";
        public string WorkshopName { get; set; } = "";
        public decimal? CostPrice { get; set; }
        public decimal? SalePrice { get; set; }
        public string? Barcode { get; set; }
        public string? UnitLabel { get; set; }
    }

    private static ParsedWorkbook? ParseWorkbook(XLWorkbook wb)
    {
        var result = new ParsedWorkbook();
        if (wb.Worksheets.Count < 2)
        {
            result.GeneralErrors.Add("Ən azı iki vərəq olmalıdır: «Kateqoriyalar», «Məhsullar».");
            return result;
        }

        var wsCat = wb.Worksheet(1);
        var wsPr = wb.Worksheet(2);

        var catHeaders = ReadHeaderMap(wsCat.Row(1));
        var prHeaders = ReadHeaderMap(wsPr.Row(1));

        var colCatName = FindColumn(catHeaders, "kateqoriya", "category");
        if (colCatName < 0)
        {
            result.GeneralErrors.Add("Kateqoriyalar: «Kateqoriya» sütunu tapılmadı.");
            return result;
        }

        var colCatParent = FindColumn(catHeaders, "valideynkateqoriya", "valideyn", "parentcategory", "parent");
        var colCatOrder = FindColumn(catHeaders, "sıra", "sira", "order", "orderindex");

        var colPrName = FindColumn(prHeaders, "məhsuladı", "mehsuladi", "productname", "product", "ad");
        var colPrCat = FindColumn(prHeaders, "kateqoriya", "category");
        var colPrWs = FindColumn(prHeaders, "emalatxana", "workshop");
        var colPrCost = FindColumn(prHeaders, "maya", "cost", "mayəqiymət", "mayeqiymet");
        var colPrSale = FindColumn(prHeaders, "satışqiyməti", "satisqiymeti", "saleprice", "satış", "satis", "qiymət", "qiymet");

        if (colPrName < 0 || colPrCat < 0 || colPrWs < 0 || colPrSale < 0)
        {
            result.GeneralErrors.Add("Məhsullar: «MəhsulAdı», «Kateqoriya», «Emalatxana», «SatışQiyməti» sütunları mütləqdir.");
            return result;
        }

        var colPrBarcode = FindColumn(prHeaders, "barkod", "barcode");
        var colPrUnit = FindColumn(prHeaders, "vahid", "unit", "salesunit");

        var lastCat = wsCat.LastRowUsed()?.RowNumber() ?? 1;
        for (var r = 2; r <= lastCat; r++)
        {
            var row = wsCat.Row(r);
            var name = GetCellString(row, colCatName);
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var parent = colCatParent > 0 ? GetCellString(row, colCatParent) : null;
            int? ord = null;
            if (colCatOrder > 0)
            {
                var cell = row.Cell(colCatOrder);
                if (!cell.IsEmpty() && cell.TryGetValue(out double d))
                    ord = (int)d;
            }

            result.CategoryRows.Add(new CategorySheetRow
            {
                Name = name,
                ParentName = string.IsNullOrWhiteSpace(parent) ? null : parent,
                OrderIndex = ord
            });
        }

        var lastPr = wsPr.LastRowUsed()?.RowNumber() ?? 1;
        for (var r = 2; r <= lastPr; r++)
        {
            var row = wsPr.Row(r);
            var name = GetCellString(row, colPrName);
            var cat = GetCellString(row, colPrCat);
            var ws = GetCellString(row, colPrWs);
            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(cat) && string.IsNullOrWhiteSpace(ws))
                continue;

            decimal? cost = null;
            if (colPrCost > 0)
            {
                var c = row.Cell(colPrCost);
                if (!c.IsEmpty() && TryReadDecimal(c, out var cv))
                    cost = cv;
            }

            decimal? sale = null;
            {
                var c = row.Cell(colPrSale);
                if (!c.IsEmpty() && TryReadDecimal(c, out var sv))
                    sale = sv;
            }

            var barcode = colPrBarcode > 0 ? GetCellString(row, colPrBarcode) : null;
            var unit = colPrUnit > 0 ? GetCellString(row, colPrUnit) : null;

            result.ProductRows.Add(new ProductSheetRow
            {
                RowNumber = r,
                NameAz = name ?? "",
                CategoryName = cat ?? "",
                WorkshopName = ws ?? "",
                CostPrice = cost,
                SalePrice = sale,
                Barcode = barcode,
                UnitLabel = string.IsNullOrWhiteSpace(unit) ? "Ədəd" : unit
            });
        }

        return result;
    }

    private static Dictionary<string, int> ReadHeaderMap(IXLRow headerRow)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in headerRow.CellsUsed())
        {
            var raw = cell.GetString().Trim();
            if (string.IsNullOrEmpty(raw))
                continue;
            var norm = NormalizeHeaderKey(raw);
            map[norm] = cell.Address.ColumnNumber;
        }

        return map;
    }

    private static int FindColumn(Dictionary<string, int> headers, params string[] normalizedAliases)
    {
        foreach (var a in normalizedAliases)
        {
            if (headers.TryGetValue(a, out var col))
                return col;
        }

        return -1;
    }

    private static string NormalizeHeaderKey(string s)
    {
        var t = s.Trim().ToLowerInvariant();
        return new string(t.Where(ch => !char.IsWhiteSpace(ch)).ToArray());
    }

    private static string GetCellString(IXLRow row, int col)
    {
        if (col < 1)
            return "";
        var v = row.Cell(col).GetString().Trim();
        return v;
    }

    private static string NormalizeName(string name) => name.Trim().ToLowerInvariant();

    private static SalesUnit MapUnit(string? label)
    {
        var n = NormalizeHeaderKey(label ?? "ədəd");
        if (n is "kq" or "kg" or "kilogram")
            return SalesUnit.Kg;
        if (n is "qram" or "qr" or "gr" or "g" or "gram")
            return SalesUnit.Gram;
        if (n is "litr" or "l" or "litre")
            return SalesUnit.Litre;
        if (n is "ml" or "millilitr" or "millilitre")
            return SalesUnit.Millilitre;
        return SalesUnit.Pcs;
    }

    private static bool TryOrderNewCategories(
        List<CategorySheetRow> rows,
        HashSet<string> existingNormalizedNames,
        out string error)
    {
        error = "";
        var toCreate = rows
            .Where(r => !string.IsNullOrWhiteSpace(r.Name))
            .GroupBy(r => NormalizeName(r.Name))
            .Select(g => g.Last())
            .Where(r => !existingNormalizedNames.Contains(NormalizeName(r.Name)))
            .ToList();

        var sim = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var n in existingNormalizedNames)
            sim[n] = Guid.Empty;

        foreach (var r in toCreate)
        {
            var k = NormalizeName(r.Name);
            if (!sim.ContainsKey(k))
                sim[k] = Guid.Empty;
        }

        var pending = toCreate.ToList();
        while (pending.Count > 0)
        {
            var batch = pending.Where(r =>
                string.IsNullOrWhiteSpace(r.ParentName) ||
                sim.ContainsKey(NormalizeName(r.ParentName!))).ToList();

            if (batch.Count == 0)
            {
                error = "Kateqoriyalar vərəqində valideyn əlaqəsi həll olunmur (tapılmayan valideyn və ya döngə).";
                return false;
            }

            foreach (var r in batch)
                pending.Remove(r);
        }

        return true;
    }

    private static List<CategorySheetRow> OrderCategoriesForInsert(List<CategorySheetRow> toCreate, Dictionary<string, Guid> nameToId)
    {
        var sim = new Dictionary<string, Guid>(nameToId, StringComparer.OrdinalIgnoreCase);
        foreach (var r in toCreate)
        {
            var k = NormalizeName(r.Name);
            if (!sim.ContainsKey(k))
                sim[k] = Guid.Empty;
        }

        var pending = toCreate.ToList();
        var ordered = new List<CategorySheetRow>();
        while (pending.Count > 0)
        {
            var batch = pending.Where(r =>
                string.IsNullOrWhiteSpace(r.ParentName) ||
                sim.ContainsKey(NormalizeName(r.ParentName!))).ToList();

            if (batch.Count == 0)
                throw new InvalidOperationException("Kateqoriya sıralaması alınmadı.");

            foreach (var r in batch)
            {
                pending.Remove(r);
                ordered.Add(r);
            }
        }

        return ordered;
    }

    private static List<MenuImportCategoryPreviewDto> BuildCategoryPreview(
        List<CategorySheetRow> rows,
        Dictionary<string, Guid> existingNameToId)
    {
        var list = new List<MenuImportCategoryPreviewDto>();
        foreach (var r in rows.Where(x => !string.IsNullOrWhiteSpace(x.Name)).GroupBy(x => NormalizeName(x.Name)).Select(g => g.Last()))
        {
            var norm = NormalizeName(r.Name);
            var exists = existingNameToId.ContainsKey(norm);
            list.Add(new MenuImportCategoryPreviewDto
            {
                NameAz = r.Name.Trim(),
                ParentName = string.IsNullOrWhiteSpace(r.ParentName) ? null : r.ParentName.Trim(),
                AlreadyExists = exists,
                WillBeCreated = !exists
            });
        }

        return list;
    }

    private static bool TryReadDecimal(IXLCell cell, out decimal value)
    {
        if (cell.DataType == XLDataType.Number && cell.TryGetValue(out double dbl))
        {
            value = (decimal)dbl;
            return true;
        }

        var s = cell.GetString().Trim();
        if (decimal.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.CurrentCulture, out value))
            return true;
        return decimal.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out value);
    }
}
