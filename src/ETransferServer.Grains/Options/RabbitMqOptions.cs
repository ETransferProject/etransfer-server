using Volo.Abp.RabbitMQ;

namespace ETransferServer.Grains.Options;

public class RabbitMqOptions : AbpRabbitMqOptions
{
    
    public StreamOption Stream { get; set; }
    
    
}

public class StreamOption
{
    
    public string ProviderName { get; set; }
    
}