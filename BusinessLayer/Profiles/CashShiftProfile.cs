using AutoMapper;
using BusinessLayer.DTOs.CashShift;
using Domain.Common.Entities;

namespace BusinessLayer.Profiles;

public class CashShiftProfile : Profile
{
    public CashShiftProfile()
    {
        CreateMap<CashShift, CashShiftGetDto>()
            .ForMember(dest => dest.OpenedByUserName, opt => opt.MapFrom(src => src.OpenedByUser.FullName))
            .ForMember(dest => dest.ClosedByUserName, opt => opt.MapFrom(src => src.ClosedByUser != null ? src.ClosedByUser.FullName : null))
            .ForMember(dest => dest.TotalCash, opt => opt.Ignore())
            .ForMember(dest => dest.TotalCard, opt => opt.Ignore())
            .ForMember(dest => dest.TotalRevenue, opt => opt.Ignore())
            .ForMember(dest => dest.OrderCount, opt => opt.Ignore());

        CreateMap<CashShiftOpenDto, CashShift>()
            .ForMember(dest => dest.StartTime, opt => opt.MapFrom(src => DateTime.UtcNow))
            .ForMember(dest => dest.IsClosed, opt => opt.MapFrom(src => false))
            .ForMember(dest => dest.OpeningDepositAmount, opt => opt.Ignore());
    }
}