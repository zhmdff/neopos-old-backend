using AutoMapper;
using BusinessLayer.DTOs.Audit;
using Domain.Entities;

namespace BusinessLayer.Profiles;

public class AuditLogProfile : Profile
{
    public AuditLogProfile()
    {
        CreateMap<AuditLogPostDto, AuditLog>()
            .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(_ => DateTime.SpecifyKind(DateTime.UtcNow.AddHours(4), DateTimeKind.Unspecified)));

        CreateMap<AuditLog, AuditLogGetDto>();
    }
}
