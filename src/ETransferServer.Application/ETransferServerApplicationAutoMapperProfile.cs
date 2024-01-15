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
using Volo.Abp.AutoMapper;


namespace ETransferServer;

public class ETransferServerApplicationAutoMapperProfile : Profile
{
    public ETransferServerApplicationAutoMapperProfile()
    {
        // CreateMap<UserSourceInput, UserGrainDto>().ReverseMap();
        // CreateMap<UserGrainDto, UserDto>().ReverseMap();
        // CreateMap<UserGrainDto, UserInformationEto>().ReverseMap();
        CreateMap<UserIndex, UserDto>().ReverseMap();
        CreateMap<AddressInfo, UserAddressInfo>().ReverseMap();

        CreateMap<UserAddress, UserAddressDto>().ReverseMap();
        
        CreateMap<UserAddressDto, UserAddress>().ReverseMap();
        CreateMap<TokenDto, Tokens.Token>().ReverseMap();

        CreateMap<FeeInfo, Fee>().ReverseMap();
        CreateMap<TransferInfo, Transfer>().ReverseMap();
        CreateMap<DepositOrderDto, DepositOrder>().ReverseMap();
        CreateMap<WithdrawOrderDto, Orders.WithdrawOrder>().ReverseMap();
        CreateMap<TokenConfig, TokenConfigDto>().ReverseMap();
        CreateMap<NetworkInfo, NetworkDto>()
            .ForMember(des => des.MultiConfirmTime, opt =>
                opt.MapFrom(src => TimeHelper.SecondsToMinute((int)src.MultiConfirmSeconds)))
            .ReverseMap();
        CreateMap<OrderStatusFlowDto, OrderStatusFlow>().ReverseMap();
        // CreateMap<WithdrawInfo, GetWithdrawInfoDto>().ReverseMap();
        // CreateMap<DepositInfo, GetDepositInfoDto>().ReverseMap();
    }
}