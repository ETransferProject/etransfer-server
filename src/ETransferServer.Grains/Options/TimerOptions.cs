namespace ETransferServer.Grains.Options;

public class TimerOptions
{
    public TimerOption WatchDogReminder { get; set; } = new(60);
    public TimerOption OrderStatusReminder { get; set; } = new()
    {
        PeriodSeconds = 60,
        DelaySeconds = 60 * 60
    };
    public TimerOption DepositOrderStatusReminder { get; set; } = new()
    {
        PeriodSeconds = 60,
        DelaySeconds = 60 * 10
    };
    public TimerOption TransferOrderStatusReminder { get; set; } = new()
    {
        PeriodSeconds = 60,
        DelaySeconds = 60 * 60 * 48
    };
    public TimerOption DepositTimer { get; set; } = new();
    public TimerOption WithdrawFromTimer { get; set; } = new();
    public TimerOption WithdrawFromFastTimer { get; set; } = new(3);
    public TimerOption CoBoDepositQueryTimer { get; set; } = new(60);
    public TimerOption TokenAddressTimer { get; set; } = new();
    public TimerOption TokenAddressRecycleTimer { get; set; } = new(60);
    public TimerOption TokenIntegrateTimer { get; set; } = new(60, 60);
    public TimerOption TokenLiquidityTimer { get; set; } = new(10, 14400);
    public TimerOption WithdrawTimer { get; set; } = new();
    public TimerOption DepositRetryTimer { get; set; } = new();
    public TimerOption WithdrawRetryTimer { get; set; } = new();
    public TimerOption WithdrawQueryTimer { get; set; } = new(60);
}


public class TimerOption
{
    public int PeriodSeconds { get; set; }
    public int DelaySeconds { get; set; }
    
    public TimerOption(int period = 10, int delaySeconds = 0)
    {
        PeriodSeconds = period;
        DelaySeconds = delaySeconds;
    }
    
}