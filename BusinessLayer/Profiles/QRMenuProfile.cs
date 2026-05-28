using AutoMapper;
using BusinessLayer.DTOs.QRMenu;
using Domain.Common.Entities;

namespace BusinessLayer.Profiles;

public class QRMenuProfile : Profile
{
    public QRMenuProfile()
    {
        // .ReverseMap() əlavə etdik ki, həm DTO -> Entity, həm də Entity -> DTO işləsin
        CreateMap<QRMenuSetting, QRMenuSettingDto>().ReverseMap();

        CreateMap<Product, ProductQRDto>();

        CreateMap<Category, CategoryQRDto>()
            .ForMember(dest => dest.Products, opt => opt.MapFrom(src =>
                src.Products.OrderBy(p => p.OrderIndexByQrMenu ?? 999)));

        CreateMap<Company, QRMenuFullDto>()
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.NameAz))
            .ForMember(dest => dest.Address, opt => opt.MapFrom(src => src.AddressAz))
            .ForMember(dest => dest.Phone1, opt => opt.MapFrom(src => src.PhoneNumber1))
            .ForMember(dest => dest.Phone2, opt => opt.MapFrom(src => src.PhoneNumber2))
            .ForMember(dest => dest.Phone3, opt => opt.MapFrom(src => src.PhoneNumber3))
            .ForMember(dest => dest.Settings, opt => opt.Ignore())
            .ForMember(dest => dest.Categories, opt => opt.Ignore());
    }
}