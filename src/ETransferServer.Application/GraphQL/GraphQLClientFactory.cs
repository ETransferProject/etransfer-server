using System;
using System.Collections.Concurrent;
using ETransferServer.Common.GraphQL;
using ETransferServer.Options;
using GraphQL.Client.Abstractions;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace ETransferServer.GraphQL
{
    public class GraphQLClientFactory : IGraphQLClientFactory, ISingletonDependency
    {
        private readonly GraphQLOptions _graphQlClientOptions;
        private readonly ConcurrentDictionary<string, Lazy<IGraphQLClient>> _clientDic;
        public GraphQLClientFactory(IOptionsSnapshot<GraphQLOptions> graphQlClientOptions)
        {
            _graphQlClientOptions = graphQlClientOptions.Value;
            _clientDic = new ConcurrentDictionary<string, Lazy<IGraphQLClient>>();
        }

        public IGraphQLClient GetClient(GraphQLClientEnum clientEnum)
        {
            var clientName = clientEnum.ToString();
            var client = _clientDic.GetOrAdd(clientName, _ => new Lazy<IGraphQLClient>(() =>
            {
                var client = new GraphQLHttpClient(_graphQlClientOptions.Configuration,
                    new NewtonsoftJsonSerializer());
                switch (clientEnum)
                {
                    case GraphQLClientEnum.SwapClient:
                        client = new GraphQLHttpClient(_graphQlClientOptions.SwapConfiguration,
                            new NewtonsoftJsonSerializer());
                        break;
                }
                return client;
            })).Value;
            return client;
        }
    }
}