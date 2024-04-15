using AElf.Contracts.MultiToken;
using AutoMapper;
using ETransferServer.Dtos.Order;
using ETransferServer.Dtos.Token;
using ETransferServer.Dtos.User;
using ETransferServer.Grains.Grain.Users;
using ETransferServer.Grains.State;
using ETransferServer.Grains.State.Order;
using ETransferServer.Grains.State.Token;
using ETransferServer.Grains.State.Users;
using ETransferServer.ThirdPart.CoBo.Dtos;
using ETransferServer.User.Dtos;
using TokenDto = ETransferServer.Dtos.Token.TokenDto;

namespace ETransferServer.Grains;

public class ETransferServerGrainsAutoMapperProfile : Profile
{
    public ETransferServerGrainsAutoMapperProfile()
    {
        CreateMap<UserGrainDto, UserState>().ReverseMap();
        CreateMap<UserState, UserDto>().ReverseMap();


        CreateMap<DepositOrderDto, DepositOrderState>().ReverseMap();
        CreateMap<OrderStatusFlowState, OrderStatusFlowDto>().ReverseMap();
        CreateMap<WithdrawOrderState, WithdrawOrderDto>().ReverseMap();

        CreateMap<TokenState, TokenDto>().ReverseMap();
        CreateMap<TokenState, TokenInfo>().ReverseMap();

        CreateMap<UserAddressDto, TokenDepositAddressState>().ReverseMap();

        CreateMap<CoBoCoinDetailDto, CoBoCoinDto>().ReverseMap();
        CreateMap<CoBoCoinState, CoBoCoinDto>().ReverseMap();
        CreateMap<CoBoCoinState, CoBoCoinDetailDto>().ReverseMap();
        CreateMap<CoBoTransactionState, CoBoTransactionDto>().ReverseMap();
    }
}