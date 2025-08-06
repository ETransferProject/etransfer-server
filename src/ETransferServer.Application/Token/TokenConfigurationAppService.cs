using System;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using ETransferServer.Common;
using ETransferServer.Grains.Grain.Token;
using ETransferServer.Grains.State.Token;
using ETransferServer.Token.Dtos;
using Microsoft.Extensions.Logging;
using Orleans;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.ObjectMapping;

namespace ETransferServer.Token;

[RemoteService(IsEnabled = false)]
[DisableAuditing]
public class TokenConfigurationAppService : ETransferServerAppService, ITokenConfigurationAppService
{
    private readonly ILogger<TokenConfigurationAppService> _logger;
    private readonly IGrainFactory _grainFactory;
    private readonly IObjectMapper _objectMapper;

    public TokenConfigurationAppService(
        ILogger<TokenConfigurationAppService> logger,
        IGrainFactory grainFactory,
        IObjectMapper objectMapper)
    {
        _logger = logger;
        _grainFactory = grainFactory;
        _objectMapper = objectMapper;
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(TokenConfigurationAppService), 
        MethodName = nameof(HandleExceptionAsync))]
    public async Task<TokenConfigurationDto> GetTokenConfigurationAsync(GetTokenConfigurationRequestDto request)
    {
        AssertHelper.NotEmpty(request.ChainId, "Chain id can not be empty.");
        AssertHelper.NotEmpty(request.Symbol, "Symbol can not be empty.");

        var grainId = ITokenConfigurationGrain.GenGrainId(request.Symbol, request.ChainId);
        var grain = _grainFactory.GetGrain<ITokenConfigurationGrain>(grainId);
        var state = await grain.GetConfigurationAsync();
        
        return _objectMapper.Map<TokenConfigurationState, TokenConfigurationDto>(state);
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(TokenConfigurationAppService), 
        MethodName = nameof(HandleExceptionAsync))]
    public async Task<TokenConfigurationDto> SetDepositEnabledAsync(string chainId, string symbol, bool enabled)
    {
        AssertHelper.NotEmpty(chainId, "Chain id can not be empty.");
        AssertHelper.NotEmpty(symbol, "Symbol can not be empty.");

        var grainId = ITokenConfigurationGrain.GenGrainId(symbol, chainId);
        var grain = _grainFactory.GetGrain<ITokenConfigurationGrain>(grainId);
        await grain.SetDepositEnabledAsync(enabled);
        
        var state = await grain.GetConfigurationAsync();
        return _objectMapper.Map<TokenConfigurationState, TokenConfigurationDto>(state);
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(TokenConfigurationAppService), 
        MethodName = nameof(HandleExceptionAsync))]
    public async Task<TokenConfigurationDto> SetWithdrawEnabledAsync(string chainId, string symbol, bool enabled)
    {
        AssertHelper.NotEmpty(chainId, "Chain id can not be empty.");
        AssertHelper.NotEmpty(symbol, "Symbol can not be empty.");

        var grainId = ITokenConfigurationGrain.GenGrainId(symbol, chainId);
        var grain = _grainFactory.GetGrain<ITokenConfigurationGrain>(grainId);
        await grain.SetWithdrawEnabledAsync(enabled);
        
        var state = await grain.GetConfigurationAsync();
        return _objectMapper.Map<TokenConfigurationState, TokenConfigurationDto>(state);
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(TokenConfigurationAppService), 
        MethodName = nameof(HandleExceptionAsync))]
    public async Task<TokenConfigurationDto> SetTransferEnabledAsync(string chainId, string symbol, bool enabled)
    {
        AssertHelper.NotEmpty(chainId, "Chain id can not be empty.");
        AssertHelper.NotEmpty(symbol, "Symbol can not be empty.");

        var grainId = ITokenConfigurationGrain.GenGrainId(symbol, chainId);
        var grain = _grainFactory.GetGrain<ITokenConfigurationGrain>(grainId);
        await grain.SetTransferEnabledAsync(enabled);
        
        var state = await grain.GetConfigurationAsync();
        return _objectMapper.Map<TokenConfigurationState, TokenConfigurationDto>(state);
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(TokenConfigurationAppService), 
        MethodName = nameof(HandleExceptionAsync))]
    public async Task<TokenConfigurationDto> SetTokenConfigurationAsync(SetTokenConfigurationDto request)
    {
        AssertHelper.NotEmpty(request.ChainId, "Chain id can not be empty.");
        AssertHelper.NotEmpty(request.Symbol, "Symbol can not be empty.");

        var grainId = ITokenConfigurationGrain.GenGrainId(request.Symbol, request.ChainId);
        var grain = _grainFactory.GetGrain<ITokenConfigurationGrain>(grainId);

        // If any configuration values are specified, perform batch setting
        if (request.DepositEnabled.HasValue || request.WithdrawEnabled.HasValue || request.TransferEnabled.HasValue)
        {
            // Get current configuration first, for unspecified values
            var currentState = await grain.GetConfigurationAsync();
            
            var depositEnabled = request.DepositEnabled ?? currentState.DepositEnabled;
            var withdrawEnabled = request.WithdrawEnabled ?? currentState.WithdrawEnabled;
            var transferEnabled = request.TransferEnabled ?? currentState.TransferEnabled;

            await grain.SetAllEnabledAsync(depositEnabled, withdrawEnabled, transferEnabled);
        }

        var state = await grain.GetConfigurationAsync();
        return _objectMapper.Map<TokenConfigurationState, TokenConfigurationDto>(state);
    }

    [ExceptionHandler(typeof(Exception),
        Message = "TokenConfiguration operation failed",
        TargetType = typeof(TokenConfigurationAppService),
        MethodName = nameof(HandleExceptionAsync))]
    public virtual async Task<FlowBehavior> HandleExceptionAsync(Exception ex)
    {
        _logger.LogError(ex, "TokenConfiguration operation failed");
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = new TokenConfigurationDto()
        };
    }
}
