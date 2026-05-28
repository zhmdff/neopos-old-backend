using AutoMapper;
using BusinessLayer.DTOs.Hall;
using Domain.Common.Entities;

namespace BusinessLayer.Profiles;

public class HallProfile : Profile
{
    public HallProfile()
    {
        CreateMap<Hall, HallGetDto>()
            .ForMember(dest => dest.TableCount, opt => opt.MapFrom(src => src.Tables.Count))
            .ForMember(dest => dest.Tables, opt => opt.MapFrom(src => src.Tables.Where(t => !t.IsDeleted)));

        CreateMap<HallPostDto, Hall>();

        CreateMap<HallPutDto, Hall>();
    }
}
