using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ETransferServer.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ETransferServer.Models;
using ETransferServer.Options;
using ETransferServer.token.Dtos;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.ObjectMapping;

namespace ETransferServer.token;

[RemoteService(IsEnabled = false)]
[DisableAuditing]
public class TokenAppService : ETransferServerAppService, ITokenAppService
{
    private readonly ILogger<TokenAppService> _logger;
    private readonly TokenOptions _tokenOptions;
    private readonly IObjectMapper _objectMapper;

    public TokenAppService(ILogger<TokenAppService> logger, IOptions<TokenOptions> tokenOptions,
        IObjectMapper objectMapper)
    {
        _logger = logger;
        _tokenOptions = tokenOptions.Value;
        _objectMapper = objectMapper;
    }

    public async Task<GetTokenListDto> GetTokenListAsync(GetTokenListRequestDto request)
    {
        try
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
                configs = _tokenOptions.Deposit[request.ChainId];
            }
            else
            {
                configs = _tokenOptions.Withdraw[request.ChainId];
            }

            var tokenDtos = _objectMapper.Map<List<TokenConfig>, List<TokenConfigDto>>(configs);
            getTokenListDto.TokenList = tokenDtos;
            getTokenListDto.ChainId = request.ChainId;
            return getTokenListDto;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "GetTokenList error");
            throw;
        }
    }
}