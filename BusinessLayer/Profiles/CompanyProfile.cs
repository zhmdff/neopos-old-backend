using AutoMapper;
using Domain.Common.Entities;
using BusinessLayer.DTOs.Company;

namespace BusinessLayer.Profiles;

public class CompanyProfile : Profile
{
    public CompanyProfile()
    {
        CreateMap<Company, CompanyGetDto>()
            .ForMember(dest => dest.Slug, opt => opt.MapFrom(src => src.Slug))
            .ForMember(dest => dest.TablesLayoutMode, opt => opt.MapFrom(src => (int)src.TablesLayoutMode));
        CreateMap<CompanyPutDto, Company>()
            .ForMember(dest => dest.TablesLayoutMode, opt => opt.Ignore());
    }
}