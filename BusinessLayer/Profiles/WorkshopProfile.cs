using AutoMapper;
using BusinessLayer.DTOs.Workshop;
using Domain.Common.Entities;

namespace BusinessLayer.Profiles;

public class WorkshopProfile : Profile
{
    public WorkshopProfile()
    {
        CreateMap<WorkshopPostDto, Workshop>();

        CreateMap<WorkshopPutDto, Workshop>();

        CreateMap<Workshop, WorkshopGetDto>();
    }
}
