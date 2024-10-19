using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Orleans.Providers.MongoDB.Configuration;
using Orleans.Providers.MongoDB.StorageProviders;
using Orleans.Providers.MongoDB.StorageProviders.Serializers;
using Orleans.Serialization;

namespace ETransferServer.Silo;

public class ETransferJsonGrainStateSerializer: IGrainStateSerializer
{
    private readonly JsonSerializerSettings jsonSettings;
    private readonly ILogger<ETransferJsonGrainStateSerializer> _logger;

    public ETransferJsonGrainStateSerializer(IOptions<JsonGrainStateSerializerOptions> options, 
        IServiceProvider serviceProvider,
        ILogger<ETransferJsonGrainStateSerializer> logger)
    {
        jsonSettings = OrleansJsonSerializerSettings.GetDefaultSerializerSettings(serviceProvider);
        options.Value.ConfigureJsonSerializerSettings(jsonSettings);
        _logger = logger;
    }

    public T Deserialize<T>(BsonValue value)
    {
        try
        {
            using var jsonReader = new JTokenReader(value.ToJToken());
            var localSerializer = JsonSerializer.CreateDefault(jsonSettings);
            return localSerializer.Deserialize<T>(jsonReader);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Silo deserialize error.");
            return default(T);
        }
    }

    public BsonValue Serialize<T>(T state)
    {
        try
        {
            var localSerializer = JsonSerializer.CreateDefault(jsonSettings);
            return JObject.FromObject(state, localSerializer).ToBson();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Silo serialize error.");
            return default(BsonValue);
        }
    }
}