using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using ETransferServer.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ETransferServer.Models;
using ETransferServer.Options;
using ETransferServer.token.Dtos;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.ObjectMapping;

namespace ETransferServer.token;

[RemoteService(IsEnabled = false)]
[DisableAuditing]
public class TokenAppService : ETransferServerAppService, ITokenAppService
{
    private readonly ILogger<TokenAppService> _logger;
    private readonly IOptionsSnapshot<TokenOptions> _tokenOptions;
    private readonly IObjectMapper _objectMapper;

    public TokenAppService(ILogger<TokenAppService> logger, IOptionsSnapshot<TokenOptions> tokenOptions,
        IObjectMapper objectMapper)
    {
        _logger = logger;
        _tokenOptions = tokenOptions;
        _objectMapper = objectMapper;
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(ExceptionHelper),
        MethodName = nameof(ExceptionHelper.HandleException))]
    public async Task<GetTokenListDto> GetTokenListAsync(GetTokenListRequestDto request)
    {
        AssertHelper.NotNull(request, "Request empty. Please refresh and try again.");
        AssertHelper.NotEmpty(request.ChainId, "Invalid chainId. Please refresh and try again.");
        AssertHelper.NotEmpty(request.Type, "Invalid type. Please refresh and try again.");
        AssertHelper.IsTrue(request.ChainId == ChainId.AELF || request.ChainId == ChainId.tDVV
                            || request.ChainId == ChainId.tDVW, "Invalid chainId value. Please refresh and try again.");
        AssertHelper.IsTrue(request.Type == OrderTypeEnum.Deposit.ToString()
                            || request.Type == OrderTypeEnum.Withdraw.ToString(), "Invalid type value. Please refresh and try again.");

        var getTokenListDto = new GetTokenListDto();
        var configs = new List<TokenConfig>();
        if (request.Type == OrderTypeEnum.Deposit.ToString())
        {
            configs = _tokenOptions.Value.Deposit[request.ChainId];
        }
        else
        {
            configs = _tokenOptions.Value.Withdraw[request.ChainId];
        }

        var tokenDtos = _objectMapper.Map<List<TokenConfig>, List<TokenConfigDto>>(configs);
        getTokenListDto.TokenList = tokenDtos;
        getTokenListDto.ChainId = request.ChainId;
        return getTokenListDto;
    }
    
    [ExceptionHandler(typeof(Exception), TargetType = typeof(ExceptionHelper),
        MethodName = nameof(ExceptionHelper.HandleException))]
    public async Task<GetTokenOptionListDto> GetTokenOptionListAsync(GetTokenOptionListRequestDto request)
    {
        AssertHelper.NotNull(request, "Request empty. Please refresh and try again.");
        AssertHelper.NotEmpty(request.Type, "Invalid type. Please refresh and try again.");
        AssertHelper.IsTrue(request.Type == OrderTypeEnum.Deposit.ToString(), "Invalid type value. Please refresh and try again.");

        var getTokenOptionListDto = new GetTokenOptionListDto();
        var depositSwapConfigs = _tokenOptions.Value.DepositSwap;
        
        var tokenOptionDtos = _objectMapper.Map<List<TokenSwapConfig>, List<TokenOptionConfigDto>>(depositSwapConfigs);

        getTokenOptionListDto.TokenList = tokenOptionDtos;
        return getTokenOptionListDto;
    }

    public bool IsValidDeposit(string toChainId, string fromSymbol, [CanBeNull] string toSymbol)
    {
        if (DepositSwapHelper.NoDepositSwap(fromSymbol, toSymbol))
        {
            return _tokenOptions.Value.DepositSwap
                .Any(config => config.Symbol == fromSymbol && config.ToTokenList.Any(token => token.Symbol == fromSymbol && token.ChainIdList.Any(chainId => chainId == toChainId)));
        }

        if (DepositSwapHelper.IsDepositSwap(fromSymbol, toSymbol))
        {
            return IsValidSwap(toChainId, fromSymbol, toSymbol);
        }

        return false;
    }

    public bool IsValidSwap(string toChainId, string fromSymbol, [CanBeNull] string toSymbol)
    {
        return DepositSwapHelper.IsDepositSwap(fromSymbol, toSymbol) && _tokenOptions.Value.DepositSwap
            .Any(config => config.Symbol == fromSymbol && config.ToTokenList.Any(token => token.Symbol == toSymbol && token.ChainIdList.Any(chainId => chainId == toChainId)));
    }
}