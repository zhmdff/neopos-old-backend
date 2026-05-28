using AutoMapper;
using Domain.Common.Entities;
using BusinessLayer.DTOs.Role;

namespace BusinessLayer.Profiles;

public class RoleProfile : Profile
{
    public RoleProfile()
    {
        CreateMap<Role, RoleGetDto>();
        CreateMap<RolePostDto, Role>()
            .ForMember(dest => dest.Permissions, opt => opt.MapFrom(src => src.Permissions));
    }
}