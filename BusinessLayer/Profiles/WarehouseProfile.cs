using AutoMapper;
using BusinessLayer.DTOs.Warehouse;
using Domain.Common.Entities;

namespace BusinessLayer.Profiles;

public class WarehouseProfile : Profile
{
    public WarehouseProfile()
    {
        CreateMap<Warehouse, WarehouseGetDto>();
        CreateMap<WarehousePostDto, Warehouse>();
    }
}
