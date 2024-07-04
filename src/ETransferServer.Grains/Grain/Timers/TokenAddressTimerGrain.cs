using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using ETransferServer.Common;
using ETransferServer.Dtos.User;
using ETransferServer.Grains.Grain.Users;
using ETransferServer.Grains.Options;
using ETransferServer.Grains.State.Users;
using ETransferServer.ThirdPart.CoBo;

namespace ETransferServer.Grains.Grain.Timers;

public interface ITokenAddressTimerGrain : IGrainWithGuidKey
{
    public Task<DateTime> GetLastCallBackTime();
}

public class TokenAddressTimerGrain: Grain<TokenAddressState>, ITokenAddressTimerGrain
{
    private DateTime _lastCallBackTime;

    private readonly IUserAddressProvider _userAddressProvider;
    private readonly ICoBoProvider _coBoProvider;
    private readonly IOptionsSnapshot<TimerOptions> _timerOptions;
    private readonly IOptionsSnapshot<DepositAddressOptions> _depositAddressOptions;
    private readonly ILogger<TokenAddressTimerGrain> _logger;
    

    public TokenAddressTimerGrain(IUserAddressProvider userAddressProvider,
        ICoBoProvider coBoProvider,
        IOptionsSnapshot<TimerOptions> timerOptions,
        IOptionsSnapshot<DepositAddressOptions> depositAddressOptions,
        ILogger<TokenAddressTimerGrain> logger)
    {
        _userAddressProvider = userAddressProvider;
        _coBoProvider = coBoProvider;
        _timerOptions = timerOptions;
        _depositAddressOptions = depositAddressOptions;
        _logger = logger;
    }

    public override async Task OnActivateAsync()
    {
        _logger.LogDebug("TokenAddressTimerGrain {Id} Activate", this.GetPrimaryKey().ToString());
        await base.OnActivateAsync();
        
        await StartTimer(TimeSpan.FromSeconds(_timerOptions.Value.TokenAddressTimer.PeriodSeconds),
            TimeSpan.FromSeconds(_timerOptions.Value.TokenAddressTimer.DelaySeconds));
    }

    private Task StartTimer(TimeSpan timerPeriod, TimeSpan delayPeriod)
    {
        _logger.LogDebug("TokenAddressTimerGrain StartTimer {StartTime}", DateTime.UtcNow.ToUtc8String());
        RegisterTimer(TimerCallback, delayPeriod, TimeSpan.Zero, timerPeriod);
        return Task.CompletedTask;
    }
    
    private async Task TimerCallback(object state)
    {
        _logger.LogDebug("TokenAddressTimerGrain callback");
        _lastCallBackTime = DateTime.UtcNow;
        
        var remainingList = await _userAddressProvider.GetRemainingAddressListAsync();
        _logger.LogDebug("RemainingList count: {count}", remainingList.Count);
        if (remainingList.Count == 0) return;
        
        foreach (var item in remainingList)
        {
            try
            {
                var split = item.Split(CommonConstant.Underline);
                if (split.Length < 2) continue;
                _logger.LogDebug("CoBoProvider.GetAddressesAsync before.");
                var addresses = await _coBoProvider.GetAddressesAsync(item, _depositAddressOptions.Value.MaxRequestNewAddressCount);
                _logger.LogDebug("CoBoProvider.GetAddressesAsync after.");
                var addressHits = new List<string>();
                var filterAddressList = await _userAddressProvider.GetAddressListAsync(addresses.Addresses);
                if (!filterAddressList.IsNullOrEmpty())
                {
                    addressHits = filterAddressList.Select(t => t.UserToken.Address).ToList();
                }

                var addressList = new List<UserAddressDto>();
                foreach (var address in addresses.Addresses)
                {
                    var addressGrain = GrainFactory.GetGrain<IUserTokenDepositAddressGrain>(address);
                    if (addressHits.Contains(address) || await addressGrain.Exist())
                    {
                        _logger.LogDebug("Address:{address} from cobo has exsited.", address);
                        continue;
                    }

                    var newAddress = new UserAddressDto()
                    {
                        Id = Guid.NewGuid(),
                        UserToken = new TokenDto()
                        {
                            Id = Guid.NewGuid(),
                            ChainId = split[0],
                            Symbol = split[1],
                            Address = address
                        },
                        CreateTime = DateTimeHelper.ToUnixTimeMilliseconds(DateTime.UtcNow),
                        UpdateTime = DateTimeHelper.ToUnixTimeMilliseconds(DateTime.UtcNow)
                    };
                    addressList.Add(newAddress);
                    await addressGrain.AddOrUpdate(newAddress);
                }
                _logger.LogDebug("AddressList count: {count}", addressList.Count);
                if (addressList.Count == 0) continue;
                await _userAddressProvider.BulkAddSync(addressList);
            }
            catch (Exception ex)
            {
                _logger.LogError("Call new addresses api failed.{message}", ex.Message);
            }
        }
    }

    public Task<DateTime> GetLastCallBackTime()
    {
        return Task.FromResult(_lastCallBackTime);
    }
}