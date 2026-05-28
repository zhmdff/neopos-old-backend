using AutoMapper;
using BusinessLayer.DTOs.Kitchen;
using BusinessLayer.DTOs.OrderDetail;
using BusinessLayer.DTOs.OrderHeader;
using BusinessLayer.DTOs.Product;
using BusinessLayer.DTOs.Workshop;
using Domain.Common.Entities;
using Domain.Entities;
using Domain.Enums;

public class OrderProfile : Profile
{
    public OrderProfile()
    {
        CreateMap<OrderHeader, OrderHeaderGetDto>()
            .ForMember(dest => dest.TableName, opt => opt.MapFrom(src => src.Table.NameAz))
            .ForMember(dest => dest.HallName, opt => opt.MapFrom(src => src.Table.Hall.NameAz))
            .ForMember(dest => dest.OrderDetails, opt => opt.MapFrom(src => src.OrderDetails))
            .ForMember(dest => dest.Customer, opt => opt.MapFrom(src => src.Customer))
            .ForMember(dest => dest.CustomerId, opt => opt.MapFrom(src => src.CustomerId))
            .ForMember(dest => dest.PaymentMethod, opt => opt.MapFrom(src =>
                src.CustomPaymentMethodId.HasValue || src.PaymentMethod == PaymentType.Custom
                    ? (int)PaymentType.Custom
                    : (src.PaymentMethod == PaymentType.Cash ||
                       src.PaymentMethod == PaymentType.Card ||
                       src.PaymentMethod == PaymentType.CashandCard)
                        ? (int)src.PaymentMethod!.Value
                        : (src.PaidCash > 0 && src.PaidCard > 0 ? 3 : (src.PaidCash > 0 ? 0 : 1))))
            .ForMember(dest => dest.CustomPaymentMethodId, opt => opt.MapFrom(src => src.CustomPaymentMethodId))
            .ForMember(dest => dest.CustomPaymentMethodName, opt => opt.MapFrom(src =>
                src.CustomPaymentMethod != null ? src.CustomPaymentMethod.NameAz : null));

        CreateMap<OrderDetail, OrderDetailGetDto>();
        CreateMap<OrderSplitPayment, OrderSplitPaymentGetDto>();

        // Product map-ini belə dəqiqləşdir:
        CreateMap<Product, ProductGetDto>()
            .ForMember(dest => dest.Workshop, opt => opt.MapFrom(src => src.Workshop));

        CreateMap<Workshop, WorkshopGetDto>();

        CreateMap<OrderDetailPostDto, OrderDetail>();
        CreateMap<OrderDetailUpdateDto, OrderDetail>()
            .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));

        CreateMap<OrderHeaderPostDto, OrderHeader>();


        CreateMap<KitchenOperation, KitchenOperationGetDto>()
            .ForMember(dest => dest.OperationType, opt => opt.MapFrom(src => src.OperationType.ToString()));

        // KitchenOperation -> KitchenPrintItemDto (Printerə gedən təmiz data)
        CreateMap<KitchenOperation, KitchenPrintItemDto>()
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.ProductName))
            .ForMember(dest => dest.Qty, opt => opt.MapFrom(src => src.Quantity))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.OperationType.ToString().ToUpper()))
            .ForMember(dest => dest.Note, opt => opt.MapFrom(src => src.OrderDetail.ItemNote));
    }
}