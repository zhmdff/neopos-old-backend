using AutoMapper;
using Domain.Entities;
using BusinessLayer.DTOs.ProductSet;

namespace BusinessLayer.Profiles;

public class ProductSetProfile : Profile
{
    public ProductSetProfile()
    {
        CreateMap<ProductSetItem, ProductSetItemGetDto>()
            .ForMember(dest => dest.ProductName, opt => opt.MapFrom(src => src.Product.NameAz))
            .ForMember(dest => dest.ProductId, opt => opt.MapFrom(src => src.ProductId))
            .ForMember(dest => dest.Quantity, opt => opt.MapFrom(src => src.Quantity));

        CreateMap<ProductSetItemPostDto, ProductSetItem>();

        CreateMap<ProductSetChoiceOption, ProductSetChoiceOptionGetDto>()
            .ForMember(dest => dest.ProductNameAz, opt => opt.MapFrom(src => src.Product.NameAz));

        CreateMap<ProductSetChoiceGroup, ProductSetChoiceGroupGetDto>()
            .ForMember(dest => dest.Options, opt => opt.MapFrom(src => src.Options.OrderBy(o => o.SortOrder).ThenBy(o => o.Product.NameAz)));

        CreateMap<ProductSet, ProductSetGetDto>()
            .ForMember(dest => dest.ProductNameAz, opt => opt.MapFrom(src => src.Product.NameAz))
            .ForMember(dest => dest.SetSalePrice, opt => opt.MapFrom(src => src.Product.SalePrice))
            .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Product.Category != null ? src.Product.Category.NameAz : null))
            .ForMember(dest => dest.CategoryId, opt => opt.MapFrom(src => src.Product.CategoryId))
            .ForMember(dest => dest.WorkshopName, opt => opt.MapFrom(src => src.Product.Workshop.NameAz))
            .ForMember(dest => dest.WorkshopId, opt => opt.MapFrom(src => src.Product.WorkshopId))
            .ForMember(dest => dest.SetItems, opt => opt.MapFrom(src => src.SetItems))
            .ForMember(dest => dest.ChoiceGroups, opt => opt.MapFrom(src => src.ChoiceGroups.OrderBy(g => g.SortOrder).ThenBy(g => g.NameAz)));

        CreateMap<ProductSetPostDto, ProductSet>()
            .ForMember(dest => dest.ChoiceGroups, opt => opt.Ignore())
            .ForMember(dest => dest.SetItems, opt => opt.MapFrom(src => src.SetItems));
    }
}