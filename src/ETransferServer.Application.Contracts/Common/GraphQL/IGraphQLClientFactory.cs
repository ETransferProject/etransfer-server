using GraphQL.Client.Abstractions;

namespace ETransferServer.Common.GraphQL;

public interface IGraphQLClientFactory
{
    IGraphQLClient GetClient(GraphQLClientEnum clientEnum);
}