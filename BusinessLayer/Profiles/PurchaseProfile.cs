using AutoMapper;
using BusinessLayer.DTOs.Purchase;
using Domain.Common.Entities;

namespace BusinessLayer.Profiles;

public class PurchaseProfile : Profile
{
    public PurchaseProfile()
    {
        CreateMap<PurchasePostDto, Purchase>()
            .ForMember(dest => dest.PurchaseItems, opt => opt.MapFrom(src => src.Items));

        CreateMap<PurchaseItemPostDto, PurchaseItem>()
            .ForMember(dest => dest.PriceAtPurchase, opt => opt.MapFrom(src => src.PriceAtPurchase));

        CreateMap<Purchase, PurchaseGetDto>()
            .ForMember(dest => dest.SupplierName, opt => opt.MapFrom(src => src.Supplier.Name))
            .ForMember(dest => dest.WarehouseName, opt => opt.MapFrom(src => src.Warehouse.Name));

        CreateMap<PurchaseItem, PurchaseItemGetDto>()
            .ForMember(dest => dest.ProductName, opt => opt.MapFrom(src => src.Product.NameAz))
            .ForMember(dest => dest.WarehouseName, opt => opt.MapFrom(src => src.Warehouse.Name))
            .ForMember(dest => dest.PriceAtPurchase, opt => opt.MapFrom(src => src.PriceAtPurchase));
    }
}