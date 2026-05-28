using AutoMapper;
using BusinessLayer.DTOs.Table;
using Domain.Common.Entities;
using Domain.Enums;

namespace BusinessLayer.Profiles;

public class TableProfile : Profile
{
    public TableProfile()
    {
        CreateMap<Table, TableGetDto>()
            .ForMember(dest => dest.HallNameAz, opt => opt.MapFrom(src => src.Hall.NameAz))
            .ForMember(dest => dest.DepositStartTime, opt => opt.MapFrom(src =>
                src.DepositStartTime.HasValue ? src.DepositStartTime.Value.ToString(@"hh\:mm") : null))
            .ForMember(dest => dest.DepositEndTime, opt => opt.MapFrom(src =>
                src.DepositEndTime.HasValue ? src.DepositEndTime.Value.ToString(@"hh\:mm") : null))
            .ForMember(dest => dest.MapShape, opt => opt.MapFrom(src => (int)src.MapShape));

        CreateMap<TablePostDto, Table>()
            .ForMember(dest => dest.Hall, opt => opt.Ignore())
            .ForMember(dest => dest.DepositStartTime, opt => opt.MapFrom(src => ParseDepositTime(src.DepositStartTime)))
            .ForMember(dest => dest.DepositEndTime, opt => opt.MapFrom(src => ParseDepositTime(src.DepositEndTime)))
            .ForMember(dest => dest.MapShape, opt => opt.MapFrom(src => ResolveMapShape(src.MapShape)));

        // Yalnız DTO-da olan sahələr; MapShape və koordinatlar göndərilməyibsə mövcud dəyər qalır.
        CreateMap<TablePutDto, Table>(MemberList.Source)
            .ForMember(dest => dest.Hall, opt => opt.Ignore())
            .ForMember(dest => dest.DepositStartTime, opt => opt.MapFrom(src => ParseDepositTime(src.DepositStartTime)))
            .ForMember(dest => dest.DepositEndTime, opt => opt.MapFrom(src => ParseDepositTime(src.DepositEndTime)))
            .ForMember(dest => dest.MapPositionX, opt =>
            {
                opt.PreCondition(src => src.MapPositionX.HasValue);
                opt.MapFrom(src => src.MapPositionX!.Value);
            })
            .ForMember(dest => dest.MapPositionY, opt =>
            {
                opt.PreCondition(src => src.MapPositionY.HasValue);
                opt.MapFrom(src => src.MapPositionY!.Value);
            })
            .ForMember(dest => dest.MapWidthPercent, opt =>
            {
                opt.PreCondition(src => src.MapWidthPercent.HasValue);
                opt.MapFrom(src => src.MapWidthPercent!.Value);
            })
            .ForMember(dest => dest.MapHeightPercent, opt =>
            {
                opt.PreCondition(src => src.MapHeightPercent.HasValue);
                opt.MapFrom(src => src.MapHeightPercent!.Value);
            })
            .ForMember(dest => dest.MapShape, opt =>
            {
                opt.PreCondition(src => src.MapShape.HasValue);
                opt.MapFrom(src => ResolveMapShape(src.MapShape));
            });
    }

    private static TimeSpan? ParseDepositTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return TimeSpan.Parse(value);
    }

    private static TableMapShape ResolveMapShape(int? shape)
    {
        if (!shape.HasValue || !Enum.IsDefined(typeof(TableMapShape), shape.Value))
            return TableMapShape.Rectangle;
        return (TableMapShape)shape.Value;
    }
}
