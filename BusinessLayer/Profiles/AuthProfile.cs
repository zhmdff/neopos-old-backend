using AutoMapper;
using Domain.Common.Entities;
using BusinessLayer.DTOs.Auth;

namespace BusinessLayer.Profiles;

public class AuthProfile : Profile
{
    public AuthProfile()
    {
        CreateMap<User, LoginResponseDTO>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.RoleName, opt => opt.MapFrom(src => src.Role.NameAz))
            .ForMember(dest => dest.CompanyName, opt => opt.MapFrom(src => src.Company.NameAz))
            .ForMember(dest => dest.PackageEndDate, opt => opt.MapFrom(src => src.Company.PackageEndDate))
            .ForMember(dest => dest.Permissions, opt => opt.MapFrom(src => src.Role.Permissions))
            .ForMember(dest => dest.RoleIsAdmin, opt => opt.MapFrom(src => src.Role.IsAdmin));
    }
}