using AutoMapper;
using BusinessLayer.DTOs.Category;
using Domain.Common.Entities;

namespace BusinessLayer.Profiles;

public class CategoryProfile : Profile
{
    public CategoryProfile()
    {
        // Entity-dən DTO-ya
        CreateMap<Category, CategoryGetDto>()
            .ForMember(dest => dest.SubCategories, opt => opt.MapFrom(src => src.SubCategories));

        // DTO-dan Entity-yə (Yaratma)
        CreateMap<CategoryPostDto, Category>();

        // DTO-dan Entity-yə (Yeniləmə)
        CreateMap<CategoryPutDto, Category>()
            .ForMember(dest => dest.Id, opt => opt.Ignore());
    }
}
