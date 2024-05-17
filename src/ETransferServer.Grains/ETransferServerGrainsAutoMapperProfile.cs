using AElf.Contracts.MultiToken;
using AutoMapper;
using ETransferServer.Dtos.GraphQL;
using ETransferServer.Dtos.Order;
using ETransferServer.Dtos.Token;
using ETransferServer.Dtos.User;
using ETransferServer.Grains.Grain.Users;
using ETransferServer.Grains.State;
using ETransferServer.Grains.State.Order;
using ETransferServer.Grains.State.Swap;
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
        CreateMap<SwapReserveState, ReserveDto>().ReverseMap().ForMember(
            destination => destination.SymbolIn,
            opt => opt.MapFrom(source => source.SymbolA))
            .ForMember(
                destination => destination.SymbolOut,
                opt => opt.MapFrom(source => source.SymbolB))
            .ForMember(
                destination => destination.ReserveIn,
                opt => opt.MapFrom(source => source.ReserveA))
            .ForMember(
                destination => destination.ReserveOut,
                opt => opt.MapFrom(source => source.ReserveB));
        // CreateMap<SwapState, DepositOrderDto>().ReverseMap().ForMember(
        //     destination => destination.SymbolIn,
        //     opt => opt.MapFrom(source => source.FromTransfer.Symbol)).ForMember(destination => destination.SymbolOut,
        //     opt => opt.MapFrom(source => source.ToTransfer.Symbol)).ForMember(destination => destination.AmountIn,
        //     opt => opt.MapFrom(source => source.FromTransfer.Amount)).ForMember(destination => destination.TimeStamp,
        //     opt => opt.MapFrom(source => source.CreateTime ?? source.FromTransfer.TxTime));
    }
}