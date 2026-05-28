using AutoMapper;
using BusinessLayer.DTOs.Supplier;
using Domain.Common.Entities;

namespace BusinessLayer.Profiles;

public class SupplierProfile : Profile
{
    public SupplierProfile()
    {
        CreateMap<Supplier, SupplierGetDto>();
        CreateMap<SupplierPostDto, Supplier>();
    }
}
