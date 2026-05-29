using Xunit;
using Moq;
using BusinessLayer.Services.Implementations;
using BusinessLayer.DTOs.OrderHeader;
using BusinessLayer.DTOs.Product;
using BusinessLayer.DTOs.Category;
using DAL.Server.Context;
using DAL.Server.Service;
using Microsoft.EntityFrameworkCore;
using AutoMapper;
using BusinessLayer.Profiles;
using BusinessLayer.Services.Abstractions;
using Domain.Entities;
using Domain.Common.Entities;
using Domain.Enums;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using Application.Interfaces;

using Microsoft.EntityFrameworkCore.Diagnostics;

namespace NeoPos.Tests;

public class FunctionalityTests
{
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;
    private readonly Mock<IAuditLogService> _auditLogMock = new();
    private readonly Mock<IHallTimeDiscountRuleService> _discountMock = new();
    private readonly Mock<ITcpPrinterService> _printerMock = new();
    private readonly Mock<ILogger<ProductService>> _productLoggerMock = new();
    private readonly Mock<ILogger<CategoryService>> _categoryLoggerMock = new();
    private readonly Mock<ICurrentUserService> _userServiceMock = new();
    private readonly Mock<IWebHostEnvironment> _envMock = new();
    private readonly Mock<ITranslationService> _translationMock = new();

    public FunctionalityTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        
        _userServiceMock.Setup(u => u.CompanyId).Returns(Guid.Empty);
        _context = new AppDbContext(options, _userServiceMock.Object);

        var config = new MapperConfiguration(cfg => {
             cfg.AddProfile<OrderProfile>();
             cfg.AddProfile<ProductProfile>();
             cfg.AddProfile<CategoryProfile>();
             cfg.AddProfile<WorkshopProfile>();
        });
        _mapper = config.CreateMapper();
    }

    private OrderService CreateOrderService()
    {
        return new OrderService(_context, _mapper, _auditLogMock.Object, _discountMock.Object, _printerMock.Object);
    }

    private ProductService CreateProductService()
    {
        return new ProductService(_context, _mapper, _envMock.Object, _translationMock.Object);
    }

    private CategoryService CreateCategoryService()
    {
        return new CategoryService(_context, _mapper, _envMock.Object, _translationMock.Object);
    }

    [Fact]
    public async Task OpenOrder_CreatesNewOrder_WhenTableIsFree()
    {
        // Arrange
        var service = CreateOrderService();
        var companyId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        
        _context.Companies.Add(new Company 
        { 
            Id = companyId, 
            NameAz = "Test Co", 
            NameEn = "Test Co", 
            NameRu = "Test Co",
            AddressAz = "Address",
            AddressEn = "Address",
            AddressRu = "Address",
            PhoneNumber1 = "123",
            Slug = "test-co"
        });
        var hall = new Hall 
        { 
            Id = Guid.NewGuid(), 
            CompanyId = companyId, 
            NameAz = "Main Hall",
            NameEn = "Main Hall",
            NameRu = "Main Hall"
        };
        _context.Halls.Add(hall);
        _context.Tables.Add(new Table 
        { 
            Id = tableId, 
            CompanyId = companyId, 
            NameAz = "Table 1", 
            NameEn = "Table 1",
            NameRu = "Table 1",
            HallId = hall.Id 
        });
        await _context.SaveChangesAsync();

        var dto = new OrderHeaderPostDto
        {
            TableId = tableId,
            CompanyId = companyId,
            CreatedBy = "TestUser"
        };

        // Act
        var result = await service.OpenOrderAsync(dto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(tableId, result.TableId);
        Assert.False(result.IsClosed);
        
        var dbOrder = await _context.OrderHeaders.FirstOrDefaultAsync(o => o.TableId == tableId);
        Assert.NotNull(dbOrder);
        Assert.Equal(companyId, dbOrder.CompanyId);
    }

    [Fact]
    public async Task GetAllProducts_ReturnsProductsForCompany()
    {
        // Arrange
        var service = CreateProductService();
        var companyId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var workshopId = Guid.NewGuid();

        _context.Workshops.Add(new Workshop 
        { 
            Id = workshopId, 
            CompanyId = companyId, 
            NameAz = "Kitchen",
            NameEn = "Kitchen",
            NameRu = "Kitchen",
            PrinterType = "Network",
            PrinterValue = "192.168.1.1"
        });
        _context.Categories.Add(new Category 
        { 
            Id = categoryId, 
            CompanyId = companyId, 
            NameAz = "Food",
            NameEn = "Food",
            NameRu = "Food"
        });
        _context.Products.Add(new Product 
        { 
            Id = Guid.NewGuid(), 
            CompanyId = companyId, 
            NameAz = "Pizza", 
            NameEn = "Pizza",
            NameRu = "Pizza",
            CategoryId = categoryId, 
            WorkshopId = workshopId,
            SalePrice = 10.5m,
            CreatedBy = "Test"
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await service.GetAllAsync(companyId, 0, 10, null, null, null);

        // Assert
        var list = new List<ProductGetDto>(result);
        Assert.Single(list);
        Assert.Equal("Pizza", list[0].NameAz);
    }

    [Fact]
    public async Task GetAllCategories_ReturnsCategoriesForCompany()
    {
        // Arrange
        var service = CreateCategoryService();
        var companyId = Guid.NewGuid();
        
        _context.Categories.Add(new Category 
        { 
            Id = Guid.NewGuid(), 
            CompanyId = companyId, 
            NameAz = "Drinks",
            NameEn = "Drinks",
            NameRu = "Drinks"
        });
        _context.Categories.Add(new Category 
        { 
            Id = Guid.NewGuid(), 
            CompanyId = companyId, 
            NameAz = "Food",
            NameEn = "Food",
            NameRu = "Food"
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await service.GetAllAsync(companyId, 0, 10, null, null);

        // Assert
        var list = new List<CategoryGetDto>(result);
        Assert.Equal(2, list.Count);
        Assert.Contains(list, c => c.NameAz == "Drinks");
        Assert.Contains(list, c => c.NameAz == "Food");
    }
}