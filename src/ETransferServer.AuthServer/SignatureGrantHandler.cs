using System.Collections.Immutable;
using System.Text;
using AElf;
using AElf.Client.Dto;
using AElf.Client.Service;
using AElf.Types;
using Google.Protobuf;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using Orleans;
using Portkey.Contracts.CA;
using ETransferServer.Auth.Dtos;
using ETransferServer.Auth.Options;
using ETransferServer.Grains.Grain.Users;
using ETransferServer.User.Dtos;
using NUglify.JavaScript.Syntax;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Identity;
using Volo.Abp.OpenIddict;
using Volo.Abp.OpenIddict.ExtensionGrantTypes;
using IdentityUser = Volo.Abp.Identity.IdentityUser;
using SignInResult = Microsoft.AspNetCore.Mvc.SignInResult;

namespace ETransferServer.Auth;

public class SignatureGrantHandler : ITokenExtensionGrant
{
    private ILogger<SignatureGrantHandler> _logger;
    private IAbpDistributedLock _distributedLock;
    private IOptionsSnapshot<ContractOptions> _contractOptions;
    private IClusterClient _clusterClient;
    private IOptionsSnapshot<GraphQlOption> _graphQlOptions;
    private IOptionsSnapshot<ChainOptions> _chainOptions;
    private readonly string _lockKeyPrefix = "ETransferServer:Auth:SignatureGrantHandler:";

    public SignatureGrantHandler(IOptionsSnapshot<GraphQlOption> graphQlOptions, ILogger<SignatureGrantHandler> logger, 
        IAbpDistributedLock distributedLock, IOptionsSnapshot<ContractOptions> contractOptions, IClusterClient clusterClient, 
        IOptionsSnapshot<ChainOptions> chainOptions)
    {
        _graphQlOptions = graphQlOptions;
        _logger = logger;
        _distributedLock = distributedLock;
        _contractOptions = contractOptions;
        _clusterClient = clusterClient;
        _chainOptions = chainOptions;
    }

    public async Task<IActionResult> HandleAsync(ExtensionGrantContext context)
    {
        var publicKeyVal = context.Request.GetParameter("pubkey").ToString();
        var signatureVal = context.Request.GetParameter("signature").ToString();
        var plainText = context.Request.GetParameter("plain_text").ToString();
        var caHash = context.Request.GetParameter("ca_hash").ToString();
        var chainId = context.Request.GetParameter("chain_id").ToString();
        var scope = context.Request.GetParameter("scope").ToString();
        var version = context.Request.GetParameter("version")?.ToString();

        var invalidParamResult = CheckParams(publicKeyVal, signatureVal, plainText, caHash, chainId, scope, version);
        if (invalidParamResult != null)
        {
            return invalidParamResult;
        }

        _logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<SignatureGrantHandler>>();
        _logger.LogInformation(
            "publicKeyVal:{publicKeyVal}, signatureVal:{signatureVal}, plainText:{plainText}, caHash:{caHash}, chainId:{chainId}, version:{version}",
            publicKeyVal, signatureVal, plainText, caHash, chainId, version);

        var rawText = Encoding.UTF8.GetString(ByteArrayHelper.HexStringToByteArray(plainText));
        var nonce = rawText.TrimEnd().Substring(plainText.IndexOf("Nonce:") + 7);

        var publicKey = ByteArrayHelper.HexStringToByteArray(publicKeyVal);
        var signature = ByteArrayHelper.HexStringToByteArray(signatureVal);
        var timestamp = long.Parse(nonce);
        var address = Address.FromPublicKey(publicKey).ToBase58();

        var time = DateTime.UnixEpoch.AddMilliseconds(timestamp);
        var timeRangeConfig = context.HttpContext.RequestServices
            .GetRequiredService<IOptionsSnapshot<TimeRangeOption>>().Value;
        _contractOptions = context.HttpContext.RequestServices.GetRequiredService<IOptionsSnapshot<ContractOptions>>()
            .Value;
        _clusterClient = context.HttpContext.RequestServices.GetRequiredService<IClusterClient>();

        _distributedLock = context.HttpContext.RequestServices.GetRequiredService<IAbpDistributedLock>();

        if (time < DateTime.UtcNow.AddMinutes(-timeRangeConfig.TimeRange) ||
            time > DateTime.UtcNow.AddMinutes(timeRangeConfig.TimeRange))
        {
            return GetForbidResult(OpenIddictConstants.Errors.InvalidRequest,
                $"The time should be {timeRangeConfig.TimeRange} minutes before and after the current time.");
        }

        var hash = Encoding.UTF8.GetBytes(plainText).ComputeHash();
        if (!AElf.Cryptography.CryptoHelper.VerifySignature(signature, hash, publicKey))
        {
            return GetForbidResult(OpenIddictConstants.Errors.InvalidRequest, "Signature validation failed.");
        }

        //Find manager by caHash
        _graphQlOptions = context.HttpContext.RequestServices.GetRequiredService<IOptionsSnapshot<GraphQlOption>>()
            .Value;
        _chainOptions = context.HttpContext.RequestServices.GetRequiredService<IOptionsSnapshot<ChainOptions>>();

        var managerCheck = await CheckAddressAsync(chainId,
   AuthConstant.PortKeyVersion2.Equals(version) ? _graphQlOptions.Url2 : _graphQlOptions.Url,
            caHash, address, version, _chainOptions.Value);
        if (!managerCheck.HasValue || !managerCheck.Value)
        {
            _logger.LogError(
                "Manager validation failed. caHash:{caHash}, address:{address}, chainId:{chainId}", caHash, address,
                chainId);
            return GetForbidResult(OpenIddictConstants.Errors.InvalidRequest, "Manager validation failed.");
        }

        var userManager = context.HttpContext.RequestServices.GetRequiredService<IdentityUserManager>();
        var user = await userManager.FindByNameAsync(caHash);
        if (user == null)
        {
            var userId = Guid.NewGuid();
            var createUserResult = await CreateUserAsync(userManager, userId, caHash, version);
            if (!createUserResult)
            {
                return GetForbidResult(OpenIddictConstants.Errors.ServerError, "Create user failed.");
            }

            user = await userManager.GetByIdAsync(userId);
        }
        else
        {
            _logger.LogInformation("check user data consistency, userId:{userId}", user.Id.ToString());
            var userGrain = _clusterClient.GetGrain<IUserGrain>(user.Id);
            var userInfo = await userGrain.GetUser();
            if (!userInfo.Success)
            {
                return GetForbidResult(OpenIddictConstants.Errors.ServerError, userInfo.Message);
            }

            if (userInfo.Data.AddressInfos.IsNullOrEmpty() || userInfo.Data.AddressInfos.Count == 1)
            {
                _logger.LogInformation("save user info into grain again, userId:{userId}", user.Id.ToString());
                var addressInfos = await GetAddressInfosAsync(caHash, version);
                await userGrain.AddOrUpdateUser(new UserGrainDto()
                {
                    UserId = user.Id,
                    CaHash = caHash,
                    AppId = AuthConstant.PortKeyAppId,
                    AddressInfos = addressInfos
                });
                _logger.LogInformation("save user success, userId:{userId}", user.Id.ToString());
            }
        }

        var userClaimsPrincipalFactory = context.HttpContext.RequestServices
            .GetRequiredService<IUserClaimsPrincipalFactory<IdentityUser>>();
        var signInManager = context.HttpContext.RequestServices.GetRequiredService<SignInManager<IdentityUser>>();
        var principal = await signInManager.CreateUserPrincipalAsync(user);
        var claimsPrincipal = await userClaimsPrincipalFactory.CreateAsync(user);
        claimsPrincipal.SetScopes("ETransferServer");
        claimsPrincipal.SetResources(await GetResourcesAsync(context, principal.GetScopes()));
        claimsPrincipal.SetAudiences("ETransferServer");

        await context.HttpContext.RequestServices.GetRequiredService<AbpOpenIddictClaimDestinationsManager>()
            .SetAsync(principal);

        return new SignInResult(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, claimsPrincipal);
    }

    private ForbidResult CheckParams(string publicKeyVal, string signatureVal, string plainText, string caHash,
        string chainId, string scope, string version)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(publicKeyVal))
        {
            errors.Add("invalid parameter publish_key.");
        }

        if (string.IsNullOrWhiteSpace(signatureVal))
        {
            errors.Add("invalid parameter signature.");
        }

        if (string.IsNullOrWhiteSpace(plainText))
        {
            errors.Add("invalid parameter plainText.");
        }

        if (string.IsNullOrWhiteSpace(caHash))
        {
            errors.Add("invalid parameter ca_hash.");
        }

        if (string.IsNullOrWhiteSpace(chainId))
        {
            errors.Add("invalid parameter chain_id.");
        }

        if (string.IsNullOrWhiteSpace(scope))
        {
            errors.Add("invalid parameter scope.");
        }
        
        if (!(string.IsNullOrWhiteSpace(version) || AuthConstant.PortKeyVersion.Equals(version) || 
              AuthConstant.PortKeyVersion2.Equals(version)))
        {
            errors.Add("invalid parameter version.");
        }

        if (errors.Count > 0)
        {
            return new ForbidResult(
                new[] { OpenIddictServerAspNetCoreDefaults.AuthenticationScheme },
                properties: new AuthenticationProperties(new Dictionary<string, string>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = OpenIddictConstants.Errors.InvalidRequest,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = GetErrorMessage(errors)
                }!));
        }

        return null;
    }

    private string GetErrorMessage(List<string> errors)
    {
        var message = string.Empty;

        errors?.ForEach(t => message += $"{t}, ");

        return message.Contains(',') ? message.TrimEnd().TrimEnd(',') : message;
    }

    private async Task<bool> CreateUserAsync(IdentityUserManager userManager, Guid userId, string caHash, string version)
    {
        var result = false;
        await using var handle =
            await _distributedLock.TryAcquireAsync(name: _lockKeyPrefix + caHash);
        //get shared lock
        if (handle != null)
        {
            var user = new IdentityUser(userId, userName: caHash, email: Guid.NewGuid().ToString("N") + "@ABP.IO");
            var identityResult = await userManager.CreateAsync(user);

            if (identityResult.Succeeded)
            {
                _logger.LogInformation("save user info into grain, userId:{userId}", userId.ToString());
                var grain = _clusterClient.GetGrain<IUserGrain>(userId);

                var addressInfos = await GetAddressInfosAsync(caHash, version);
                await grain.AddOrUpdateUser(new UserGrainDto()
                {
                    UserId = userId,
                    CaHash = caHash,
                    AppId = AuthConstant.PortKeyAppId,
                    AddressInfos = addressInfos
                });
                _logger.LogInformation("create user success, userId:{userId}", userId.ToString());
            }

            result = identityResult.Succeeded;
        }
        else
        {
            _logger.LogError("do not get lock, keys already exits, userId:{userId}", userId.ToString());
        }

        return result;
    }

    private async Task<List<AddressInfo>> GetAddressInfosAsync(string caHash, string version)
    {
        var addressInfos = new List<AddressInfo>();
        var holderInfoDto =
            await GetHolderInfosAsync(
                AuthConstant.PortKeyVersion2.Equals(version) ? _graphQlOptions.Url2 : _graphQlOptions.Url, caHash);

        var chainIds = new List<string>();
        if (holderInfoDto != null && !holderInfoDto.CaHolderInfo.IsNullOrEmpty())
        {
            addressInfos.AddRange(holderInfoDto.CaHolderInfo.Select(t => new AddressInfo
                { ChainId = t.ChainId, Address = t.CaAddress }));
            chainIds = holderInfoDto.CaHolderInfo.Select(t => t.ChainId).ToList();
        }

        var chains = _chainOptions.Value.ChainInfos.Select(key => _chainOptions.Value.ChainInfos[key.Key])
            .Select(chainOptionsChainInfo => chainOptionsChainInfo.ChainId).Where(t => !chainIds.Contains(t));

        foreach (var chainId in chains)
        {
            try
            {
                var addressInfo = await GetAddressInfoAsync(chainId, caHash, version);
                addressInfos.Add(addressInfo);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "get holder from chain error, caHash:{caHash}", caHash);
            }
        }

        return addressInfos;
    }

    private async Task<AddressInfo> GetAddressInfoAsync(string chainId, string caHash, string version)
    {
        var param = new GetHolderInfoInput
        {
            CaHash = Hash.LoadFromHex(caHash),
            LoginGuardianIdentifierHash = Hash.Empty
        };

        var output =
            await CallTransactionAsync<GetHolderInfoOutput>(chainId, AuthConstant.GetHolderInfo, version, 
                param, false, _chainOptions.Value);

        return new AddressInfo()
        {
            Address = output.CaAddress.ToBase58(),
            ChainId = chainId
        };
    }

    private async Task<HolderInfoIndexerDto> GetHolderInfosAsync(string url, string caHash)
    {
        using var graphQlClient = new GraphQLHttpClient(url, new NewtonsoftJsonSerializer());
        var request = new GraphQLRequest
        {
            Query = @"
			    query($caHash:String,$skipCount:Int!,$maxResultCount:Int!) {
                    caHolderInfo(dto: {caHash:$caHash,skipCount:$skipCount,maxResultCount:$maxResultCount}){
                            id,chainId,caHash,caAddress,originChainId,managerInfos{address,extraData}}
                }",
            Variables = new
            {
                caHash, skipCount = 0, maxResultCount = 10
            }
        };

        var graphQlResponse = await graphQlClient.SendQueryAsync<HolderInfoIndexerDto>(request);
        return graphQlResponse.Data;
    }

    private async Task<bool?> CheckAddressAsync(string chainId, string graphQlUrl, string caHash, string manager,
        string version, ChainOptions chainOptions)
    {
        var graphQlResult = await CheckAddressFromGraphQlAsync(graphQlUrl, caHash, manager);
        if (!graphQlResult.HasValue || !graphQlResult.Value)
        {
            _logger.LogDebug("graphql is invalid.");
            return await CheckAddressFromContractAsync(chainId, caHash, manager, version, chainOptions);
        }

        return true;
    }

    private async Task<bool?> CheckAddressFromGraphQlAsync(string url, string caHash,
        string managerAddress)
    {
        var cHolderInfos = await GetHolderInfosAsync(url, caHash);
        var caHolder = cHolderInfos?.CaHolderInfo?.SelectMany(t=>t.ManagerInfos);
        return caHolder?.Any(t => t.Address == managerAddress);
    }

    private async Task<bool?> CheckAddressFromContractAsync(string chainId, string caHash, string manager, string version,
        ChainOptions chainOptions)
    {
        var param = new GetHolderInfoInput
        {
            CaHash = Hash.LoadFromHex(caHash),
            LoginGuardianIdentifierHash = Hash.Empty
        };

        var output =
            await CallTransactionAsync<GetHolderInfoOutput>(chainId, AuthConstant.GetHolderInfo, version, 
                param, false, chainOptions);

        return output?.ManagerInfos?.Any(t => t.Address.ToBase58() == manager);
    }

    private async Task<T> CallTransactionAsync<T>(string chainId, string methodName, string version, IMessage param,
        bool isCrossChain, ChainOptions chainOptions) where T : class, IMessage<T>, new()
    {
        try
        {
            var chainInfo = chainOptions.ChainInfos[chainId];

            var client = new AElfClient(chainInfo.BaseUrl);
            await client.IsConnectedAsync();
            var address = client.GetAddressFromPrivateKey(_contractOptions.CommonPrivateKeyForCallTx);

            var contractAddress = isCrossChain
                ? (await client.GetContractAddressByNameAsync(HashHelper.ComputeFrom(ContractName.CrossChain)))
                .ToBase58()
                : AuthConstant.PortKeyVersion2.Equals(version) ? chainInfo.ContractAddress2: chainInfo.ContractAddress;

            var transaction =
                await client.GenerateTransactionAsync(address, contractAddress,
                    methodName, param);

            var txWithSign = client.SignTransaction(_contractOptions.CommonPrivateKeyForCallTx, transaction);
            var result = await client.ExecuteTransactionAsync(new ExecuteTransactionDto
            {
                RawTransaction = txWithSign.ToByteArray().ToHex()
            });

            var value = new T();
            value.MergeFrom(ByteArrayHelper.HexStringToByteArray(result));
            return value;
        }
        catch (Exception e)
        {
            if (methodName != AuthConstant.GetHolderInfo)
            {
                _logger.LogError(e, "CallTransaction error, chain id:{chainId}, methodName:{methodName}", chainId,
                    methodName);
            }

            _logger.LogError(e, "CallTransaction error, chain id:{chainId}, methodName:{methodName}", chainId,
                methodName);
            return null;
        }
    }

    private async Task<CAHolderManagerInfo> GetManagerList(string url, string caHash)
    {
        using var graphQlClient = new GraphQLHttpClient(url, new NewtonsoftJsonSerializer());

        var request = new GraphQLRequest
        {
            Query =
                "query{caHolderManagerInfo(dto: {skipCount:0,maxResultCount:10,caHash:\"" + caHash +
                "\"}){chainId,caHash,caAddress,managerInfos{address,extraData}}}"
        };

        var graphQlResponse = await graphQlClient.SendQueryAsync<CAHolderManagerInfo>(request);
        return graphQlResponse.Data;
    }

    private ForbidResult GetForbidResult(string errorType, string errorDescription)
    {
        return new ForbidResult(
            new[] { OpenIddictServerAspNetCoreDefaults.AuthenticationScheme },
            properties: new AuthenticationProperties(new Dictionary<string, string>
            {
                [OpenIddictServerAspNetCoreConstants.Properties.Error] = errorType,
                [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = errorDescription
            }!));
    }

    private async Task<IEnumerable<string>> GetResourcesAsync(ExtensionGrantContext context,
        ImmutableArray<string> scopes)
    {
        var resources = new List<string>();
        if (!scopes.Any())
        {
            return resources;
        }

        await foreach (var resource in context.HttpContext.RequestServices.GetRequiredService<IOpenIddictScopeManager>()
                           .ListResourcesAsync(scopes))
        {
            resources.Add(resource);
        }

        return resources;
    }

    public string Name { get; } = "signature";
}