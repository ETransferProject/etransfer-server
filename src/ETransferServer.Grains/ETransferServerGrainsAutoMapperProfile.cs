using AElf.Contracts.MultiToken;
using AutoMapper;
using ETransferServer.Dtos.GraphQL;
using ETransferServer.Dtos.Order;
using ETransferServer.Dtos.Token;
using ETransferServer.Dtos.TokenAccess;
using ETransferServer.Dtos.User;
using ETransferServer.Etos.Order;
using ETransferServer.Grains.Grain.Users;
using ETransferServer.Grains.State;
using ETransferServer.Grains.State.Order;
using ETransferServer.Grains.State.Swap;
using ETransferServer.Grains.State.Token;
using ETransferServer.Grains.State.Users;
using ETransferServer.Orders;
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
        CreateMap<UserReconciliationState, UserReconciliationDto>().ReverseMap();

        CreateMap<DepositOrderDto, DepositOrderState>().ReverseMap();
        CreateMap<DepositOrderDto, OrderChangeEto>().ReverseMap();
        CreateMap<OrderStatusFlowState, OrderStatusFlowDto>().ReverseMap();
        CreateMap<WithdrawOrderState, WithdrawOrderDto>().ReverseMap();
        CreateMap<WithdrawOrderDto, OrderChangeEto>().ReverseMap();
        CreateMap<Transfer, TransferInfo>().ReverseMap();

        CreateMap<TokenState, TokenDto>().ReverseMap();
        CreateMap<TokenState, TokenInfo>().ReverseMap();
        CreateMap<TokenOwnerRecordState, TokenOwnerListDto>().ReverseMap();
        CreateMap<UserTokenOwnerState, TokenOwnerListDto>().ReverseMap();
        CreateMap<UserTokenAccessInfoState, UserTokenAccessInfoDto>().ReverseMap();
        CreateMap<UserTokenApplyOrderState, TokenApplyOrderDto>().ReverseMap();
        CreateMap<TokenApplyOrderResultDto, TokenApplyOrderDto>().ReverseMap();
        CreateMap<UserTokenIssueState, UserTokenIssueDto>().ReverseMap();

        CreateMap<UserAddressDto, TokenDepositAddressState>().ReverseMap();
        CreateMap<UserAddressDto, UserDepositAddressState>().ReverseMap();

        CreateMap<CoBoCoinDetailDto, CoBoCoinDto>().ReverseMap();
        CreateMap<CoBoCoinState, CoBoCoinDto>().ReverseMap();
        CreateMap<CoBoCoinState, CoBoCoinDetailDto>().ReverseMap();
        CreateMap<CoBoAccountState, AssetDto>().ReverseMap();
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