using AutoMapper;
using ETransferServer.Common;
using ETransferServer.Dtos.Order;
using ETransferServer.Dtos.User;
using ETransferServer.Entities;
using ETransferServer.Orders;
using ETransferServer.User.Dtos;
using ETransferServer.Network.Dtos;
using ETransferServer.Options;
using ETransferServer.Users;
using ETransferServer.token.Dtos;

namespace ETransferServer;

public class ETransferServerApplicationAutoMapperProfile : Profile
{
    public ETransferServerApplicationAutoMapperProfile()
    {
        CreateMap<UserIndex, UserDto>().ReverseMap();
        CreateMap<AddressInfo, UserAddressInfo>().ReverseMap();
        CreateMap<UserAddress, UserAddressDto>().ReverseMap();
        CreateMap<TokenDto, Tokens.Token>().ReverseMap();

        CreateMap<FeeInfo, Fee>().ReverseMap();
        CreateMap<TransferInfo, Transfer>().ReverseMap();
        CreateMap<DepositOrderDto, OrderIndex>().ReverseMap();
        CreateMap<WithdrawOrderDto, OrderIndex>().ReverseMap();
        CreateMap<OrderIndex, OrderIndexDto>().ReverseMap();
        CreateMap<Transfer, TransferInfoDto>().ReverseMap();
        CreateMap<TokenConfig, TokenConfigDto>().ReverseMap();
        CreateMap<TokenSwapConfig, TokenOptionConfigDto>().ReverseMap();
        CreateMap<NetworkInfo, NetworkDto>()
            .ForMember(des => des.MultiConfirmTime, opt =>
                opt.MapFrom(src => TimeHelper.SecondsToMinute((int)src.MultiConfirmSeconds)))
            .ReverseMap();
        CreateMap<OrderStatusFlowDto, OrderStatusFlow>().ReverseMap();
    }
}