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
using ETransferServer.Token.Dtos;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.ObjectMapping;

namespace ETransferServer.Token;

[RemoteService(IsEnabled = false)]
[DisableAuditing]
public partial class TokenAppService : ETransferServerAppService, ITokenAppService
{
    private readonly ILogger<TokenAppService> _logger;
    private readonly IOptionsSnapshot<TokenInfoOptions> _tokenInfoOptions;
    private readonly IObjectMapper _objectMapper;
    private readonly ISupportedChainTokenProvider _supportedChainTokenProvider;

    public TokenAppService(ILogger<TokenAppService> logger,
        IOptionsSnapshot<TokenInfoOptions> tokenInfoOptions,
        IObjectMapper objectMapper, ISupportedChainTokenProvider supportedChainTokenProvider)
    {
        _logger = logger;
        _tokenInfoOptions = tokenInfoOptions;
        _objectMapper = objectMapper;
        _supportedChainTokenProvider = supportedChainTokenProvider;
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(TokenAppService),
        MethodName = nameof(HandleListExceptionAsync))]
    public async Task<GetTokenListDto> GetTokenListAsync(GetTokenListRequestDto request)
    {
        AssertHelper.NotNull(request, "Request empty. Please refresh and try again.");
        AssertHelper.NotEmpty(request.Type, "Invalid type. Please refresh and try again.");
        if (request.Type == OrderTypeEnum.Deposit.ToString() || request.Type == OrderTypeEnum.Withdraw.ToString())
        {
            AssertHelper.NotEmpty(request.ChainId, "Invalid chainId. Please refresh and try again.");
            AssertHelper.IsTrue(request.ChainId == ChainId.AELF || request.ChainId == ChainId.tDVV
                      || request.ChainId == ChainId.tDVW, "Invalid chainId value. Please refresh and try again.");
        }
        AssertHelper.IsTrue(request.Type == OrderTypeEnum.Deposit.ToString()
                            || request.Type == OrderTypeEnum.Withdraw.ToString()
                            || request.Type == OrderTypeEnum.Transfer.ToString(), "Invalid type value. Please refresh and try again.");

        var getTokenListDto = new GetTokenListDto();
        var tokenList = _supportedChainTokenProvider.GetTokenListByType(request.Type, request.ChainId);
        getTokenListDto.TokenList = tokenList;
        getTokenListDto.ChainId = request.ChainId;
        return getTokenListDto;
    }
    
    [ExceptionHandler(typeof(Exception), TargetType = typeof(TokenAppService),
        MethodName = nameof(HandleOptionExceptionAsync))]
    public async Task<GetTokenOptionListDto> GetTokenOptionListAsync(GetTokenOptionListRequestDto request)
    {
        AssertHelper.NotNull(request, "Request empty. Please refresh and try again.");
        AssertHelper.NotEmpty(request.Type, "Invalid type. Please refresh and try again.");
        AssertHelper.IsTrue(request.Type == OrderTypeEnum.Deposit.ToString(), "Invalid type value. Please refresh and try again.");

        var res = _supportedChainTokenProvider.GetTokenConfig();
        var getTokenOptionListDto = new GetTokenOptionListDto
        {
            TokenList = res
        };
        return getTokenOptionListDto;
    }

    public bool IsValidDeposit(string toChainId, string fromSymbol, [CanBeNull] string toSymbol)
    {
        return _supportedChainTokenProvider.IsTokenSupportedDeposit(fromSymbol, toSymbol, toChainId);
    }

    public bool IsValidSwap(string toChainId, string fromSymbol, [CanBeNull] string toSymbol)
    {
        return _supportedChainTokenProvider.IsTokenSupportedSwap(fromSymbol, toSymbol, toChainId);
    }

    public async Task<TokenInfoDto> GetTokenInfoAsync(string symbol)
    {
        return _tokenInfoOptions.Value.Tokens[symbol];
    }
}