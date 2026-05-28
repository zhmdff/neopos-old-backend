using AutoMapper;
using BusinessLayer.DTOs.Product;
using BusinessLayer.DTOs.ProductVariant;
using Domain.Common.Entities;
using Domain.Enums;
using System.Linq;

namespace BusinessLayer.Profiles;

public class ProductProfile : Profile
{
    public ProductProfile()
    {
        CreateMap<Product, ProductGetDto>()
            .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Category != null ? src.Category.NameAz : null))
            .ForMember(dest => dest.WorkshopName, opt => opt.MapFrom(src => src.Workshop.NameAz))
            .ForMember(dest => dest.DeliveryPrice, opt => opt.MapFrom(src => src.DeliveryPrice))
            .ForMember(dest => dest.Variants, opt => opt.MapFrom(src => src.Variants.Where(v => !v.IsDeleted)))
            .ForMember(dest => dest.AdditionalWorkshopIds, opt => opt.MapFrom(src =>
                src.AdditionalWorkshops.Select(x => x.WorkshopId)));

        CreateMap<ProductPostDto, Product>()
            .ForMember(dest => dest.SalePrice, opt => opt.Ignore())
            .ForMember(dest => dest.DeliveryPrice, opt => opt.MapFrom(src => src.DeliveryPrice))
            // Nullable DTO → bool entity: null gələndə default-ları pozmamaq üçün ignore edirik,
            // dəyəri service səviyyəsində set edəcəyik.
            .ForMember(dest => dest.ShowInQr, opt => opt.Ignore())
            .ForMember(dest => dest.ShowInTerminal, opt => opt.Ignore());

        CreateMap<ProductPutDto, Product>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.SalePrice, opt => opt.Ignore())
            .ForMember(dest => dest.DeliveryPrice, opt => opt.MapFrom(src => src.DeliveryPrice))
            .ForMember(dest => dest.ShowInQr, opt => opt.Ignore())
            .ForMember(dest => dest.ShowInTerminal, opt => opt.Ignore());

        CreateMap<ProductVariant, ProductVariantGetDto>();
    }
}