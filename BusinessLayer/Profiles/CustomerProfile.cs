using AutoMapper;
using BusinessLayer.DTOs.Customer;
using Domain.Entities;

namespace BusinessLayer.Profiles;

public class CustomerProfile : Profile
{
    public CustomerProfile()
    {
        CreateMap<Customer, CustomerGetDto>();
    }
}
