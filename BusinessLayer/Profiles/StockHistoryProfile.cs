using AutoMapper;
using BusinessLayer.DTOs.ProductStockHistory;
using Domain.Common.Entities;

namespace BusinessLayer.Profiles;

public class StockHistoryProfile : Profile
{
    public StockHistoryProfile()
    {
        CreateMap<ProductStockHistory, StockHistoryGetDto>()
            .ForMember(dest => dest.ProductName, opt => opt.MapFrom(src => src.Product.NameAz))
            .ForMember(dest => dest.WarehouseName, opt => opt.MapFrom(src => src.Warehouse.Name))
            .ForMember(dest => dest.SupplierName, opt => opt.MapFrom(src => src.Supplier != null ? src.Supplier.Name : "Daxili Hərəkət"))
            .ForMember(dest => dest.MovementTypeName, opt => opt.MapFrom(src => src.MovementType.ToString()));
    }
}