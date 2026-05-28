using AutoMapper;
using Domain.Common.Entities;
using BusinessLayer.DTOs.User;

namespace BusinessLayer.Profiles;

public class UserProfile : Profile
{
    public UserProfile()
    {
        CreateMap<User, UserGetDto>()
            .ForMember(dest => dest.PanelPassword, opt => opt.Ignore())
            .ForMember(dest => dest.RoleNameAz, opt => opt.MapFrom(src => src.Role.NameAz))
            .ForMember(dest => dest.RoleIsAdmin, opt => opt.MapFrom(src => src.Role.IsAdmin))
            .ForMember(dest => dest.Permissions, opt => opt.MapFrom(src => src.Role.Permissions));
        CreateMap<UserPostDto, User>();
        CreateMap<UserPutDto, User>()
            .ForMember(dest => dest.PasswordHash, opt => opt.Ignore());
    }
}