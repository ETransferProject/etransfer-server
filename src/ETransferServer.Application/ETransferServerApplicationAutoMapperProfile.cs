using AutoMapper;
using ETransferServer.Common;
using ETransferServer.Dtos.Info;
using ETransferServer.Dtos.Order;
using ETransferServer.Dtos.Reconciliation;
using ETransferServer.Dtos.TokenAccess;
using ETransferServer.Dtos.User;
using ETransferServer.Entities;
using ETransferServer.Etos.Order;
using ETransferServer.Models;
using ETransferServer.Orders;
using ETransferServer.User.Dtos;
using ETransferServer.Network.Dtos;
using ETransferServer.Options;
using ETransferServer.Users;
using ETransferServer.Token.Dtos;
using ETransferServer.TokenAccess;
using ETransferServer.Withdraw.Dtos;
using ETransferServer.WithdrawOrder.Dtos;

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
        CreateMap<OrderIndex, OrderRecordDto>().ReverseMap();
        CreateMap<OrderIndexDto, OrderDetailDto>().ReverseMap();
        CreateMap<OrderRecordDto, OrderMoreDetailDto>().ReverseMap();
        CreateMap<OrderDetailDto, OrderMoreDetailDto>().ReverseMap();
        CreateMap<Transfer, TransferInfoDto>().ReverseMap();
        CreateMap<TokenConfig, TokenConfigDto>().ReverseMap();
        CreateMap<TokenConfig, TokenConfigOptionDto>().ReverseMap();
        CreateMap<TokenSwapConfig, TokenOptionConfigDto>().ReverseMap();
        CreateMap<ToTokenConfig, ToTokenOptionConfigDto>().ReverseMap();
        CreateMap<ToTokenConfig, TokenConfig>().ReverseMap();
        
        CreateMap<UserTokenAccessInfoIndex, UserTokenAccessInfoDto>().ReverseMap();
        CreateMap<UserTokenAccessInfoInput, UserTokenAccessInfoDto>().ReverseMap();
        CreateMap<UserTokenAccessInfoDto, UserTokenAccessInfoIndex>().ReverseMap();
        CreateMap<TokenApplyOrderIndex, TokenApplyOrderDto>().ReverseMap();
        CreateMap<NetworkInfo, NetworkDto>()
            .ForMember(des => des.MultiConfirmTime, opt =>
                opt.MapFrom(src => TimeHelper.SecondsToMinute((int)src.MultiConfirmSeconds)))
            .ReverseMap();
        CreateMap<NetworkInfo, NetworkOptionDto>().ReverseMap();
        CreateMap<OrderStatusFlowDto, OrderStatusFlow>().ReverseMap();
        CreateMap<OrderChangeEto, OrderIndex>().ReverseMap();
        CreateMap<WithdrawInfoDto, TransferDetailInfoDto>().ReverseMap();
        CreateMap<GetTransferListRequestDto, GetWithdrawListRequestDto>().ForMember(
                destination => destination.ChainId,
                opt => opt.MapFrom(source => source.FromNetwork))
            .ForMember(
                destination => destination.Network,
                opt => opt.MapFrom(source => source.ToNetwork))
            .ForMember(
                destination => destination.Address,
                opt => opt.MapFrom(source => source.ToAddress));
        CreateMap<CreateWithdrawOrderDto, CreateTransferOrderDto>().ReverseMap();
        CreateMap<GetTransferOrderRequestDto, GetWithdrawOrderRequestDto>().ForMember(
                destination => destination.FromChainId,
                opt => opt.MapFrom(source => source.FromNetwork))
            .ForMember(
                destination => destination.Network,
                opt => opt.MapFrom(source => source.ToNetwork))
            .ForMember(
                destination => destination.Symbol,
                opt => opt.MapFrom(source => source.FromSymbol));
    }
}