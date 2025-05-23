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
using Portkey.Contracts.CA;
using ETransferServer.Auth.Dtos;
using ETransferServer.Auth.Options;
using ETransferServer.Common;
using ETransferServer.Grains.Grain.Users;
using ETransferServer.User.Dtos;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Identity;
using Volo.Abp.OpenIddict;
using Volo.Abp.OpenIddict.ExtensionGrantTypes;
using IdentityUser = Volo.Abp.Identity.IdentityUser;
using SignInResult = Microsoft.AspNetCore.Mvc.SignInResult;

namespace ETransferServer.Auth;

public partial class SignatureGrantHandler : ITokenExtensionGrant
{
    private ILogger<SignatureGrantHandler> _logger;
    private IAbpDistributedLock _distributedLock;
    private IOptionsSnapshot<ContractOptions> _contractOptions;
    private IClusterClient _clusterClient;
    private IOptionsSnapshot<GraphQlOption> _graphQlOptions;
    private IOptionsSnapshot<ChainOptions> _chainOptions;
    private IOptionsSnapshot<DidServerOptions> _didServerOptions;
    private IOptionsSnapshot<RecaptchaOptions> _recaptchaOptions;
    private HttpClient _httpClient;
    private readonly string _lockKeyPrefix = "ETransferServer:Auth:SignatureGrantHandler:";

    public async Task<IActionResult> HandleAsync(ExtensionGrantContext context)
    {
        var publicKeyVal = context.Request.GetParameter("pubkey").ToString();
        var signatureVal = context.Request.GetParameter("signature").ToString();
        var plainText = context.Request.GetParameter("plain_text").ToString();
        var caHash = context.Request.GetParameter("ca_hash")?.ToString();
        var chainId = context.Request.GetParameter("chain_id")?.ToString();
        var scope = context.Request.GetParameter("scope").ToString();
        var version = context.Request.GetParameter("version")?.ToString();
        var source = context.Request.GetParameter("source").ToString();
        var sourceType = context.Request.GetParameter("sourceType")?.ToString();
        var recaptchaToken = context.Request.GetParameter("recaptchaToken")?.ToString();

        _logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<SignatureGrantHandler>>();
        _logger.LogInformation(
            "before publicKeyVal:{publicKeyVal}, signatureVal:{signatureVal}, plainText:{plainText}, caHash:{caHash}, chainId:{chainId}, version:{version}, source:{source}, sourceType:{sourceType}, recaptchaToken: {recaptchaToken}",
            publicKeyVal, signatureVal, plainText, caHash, chainId, version, source, sourceType, recaptchaToken);
        
        var invalidParamResult = CheckParams(publicKeyVal, signatureVal, plainText, caHash, chainId, scope, version, source, sourceType);
        if (invalidParamResult != null)
        {
            return invalidParamResult;
        }

        _logger.LogInformation(
            "publicKeyVal:{publicKeyVal}, signatureVal:{signatureVal}, plainText:{plainText}, caHash:{caHash}, chainId:{chainId}, version:{version}, source:{source}",
            publicKeyVal, signatureVal, plainText, caHash, chainId, version, source);

        var rawText = Encoding.UTF8.GetString(ByteArrayHelper.HexStringToByteArray(plainText));
        _logger.LogInformation("rawText:{rawText}", rawText);
        var nonce = rawText.TrimEnd().Substring(rawText.LastIndexOf("Nonce:") + 6);
        _logger.LogInformation("nonce:{nonce}", nonce);
        var publicKey = ByteArrayHelper.HexStringToByteArray(publicKeyVal);
        _logger.LogInformation("publicKey:{publicKey}", publicKey);
        var signature = ByteArrayHelper.HexStringToByteArray(signatureVal);
        _logger.LogInformation("signature:{signature}", signature);
        var timestamp = long.Parse(nonce);
        _logger.LogInformation("timestamp:{timestamp}", timestamp);
        var time = DateTime.UnixEpoch.AddMilliseconds(timestamp);
        _logger.LogInformation("time:{time}", time);
        var timeRangeConfig = context.HttpContext.RequestServices
            .GetRequiredService<IOptionsSnapshot<TimeRangeOption>>().Value;

        if (time < DateTime.UtcNow.AddMinutes(-timeRangeConfig.TimeRange) ||
            time > DateTime.UtcNow.AddMinutes(timeRangeConfig.TimeRange))
        {
            return GetForbidResult(OpenIddictConstants.Errors.InvalidRequest,
                $"The time should be {timeRangeConfig.TimeRange} minutes before and after the current time.");
        }

        if (!await CheckSignature(source, sourceType, signature, plainText, publicKey))
        {
            return GetForbidResult(OpenIddictConstants.Errors.InvalidRequest, "Signature validation failed.");
        }

        //Find manager by caHash
        _contractOptions = context.HttpContext.RequestServices.GetRequiredService<IOptionsSnapshot<ContractOptions>>();
        _clusterClient = context.HttpContext.RequestServices.GetRequiredService<IClusterClient>();
        _distributedLock = context.HttpContext.RequestServices.GetRequiredService<IAbpDistributedLock>();
        _graphQlOptions = context.HttpContext.RequestServices.GetRequiredService<IOptionsSnapshot<GraphQlOption>>();
        _chainOptions = context.HttpContext.RequestServices.GetRequiredService<IOptionsSnapshot<ChainOptions>>();
        _didServerOptions = context.HttpContext.RequestServices.GetRequiredService<IOptionsSnapshot<DidServerOptions>>();
        _recaptchaOptions =
            context.HttpContext.RequestServices.GetRequiredService<IOptionsSnapshot<RecaptchaOptions>>();
        
        IdentityUser user = null;
        var address = string.Empty;
        if (source == AuthConstant.PortKeySource || source == AuthConstant.NightElfSource)
        {
            address = Address.FromPublicKey(publicKey).ToBase58();
            _logger.LogInformation("address:{address}", address);
        }

        _logger.LogInformation("before create User, source: {source}", source);
        if (source == AuthConstant.PortKeySource)
        {
            _httpClient = context.HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient();
            var managerCheck = await CheckAddressAsync(chainId,
                AuthConstant.PortKeyVersion2.Equals(version) ? _graphQlOptions.Value.Url2 : _graphQlOptions.Value.Url,
                caHash, address, version, _chainOptions.Value);
            if (!managerCheck.HasValue || !managerCheck.Value)
            {
                _logger.LogError(
                    "Manager validation failed. caHash:{caHash}, address:{address}, chainId:{chainId}", caHash, address,
                    chainId);
                return GetForbidResult(OpenIddictConstants.Errors.InvalidRequest, "Manager validation failed.");
            }

            _logger.LogInformation("before  var userManager = context.HttpContext.RequestServices.GetRequiredService<IdentityUserManager>();");
            var userManager = context.HttpContext.RequestServices.GetRequiredService<IdentityUserManager>();
            user = await userManager.FindByNameAsync(caHash);
            if (user == null)
            {
                _logger.LogInformation("before  CreatePortKeyUserAsync(userManager, userId, caHash, version)");
                var userId = await GetUserIdAsync(chainId, caHash, version);
                var createUserResult = await CreatePortKeyUserAsync(userManager, userId, caHash, version);
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
        }
        else if (source == AuthConstant.NightElfSource)
        {
            var userManager = context.HttpContext.RequestServices.GetRequiredService<IdentityUserManager>();
            user = await userManager.FindByNameAsync(address);
            if (user == null)
            {
                _httpClient = context.HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient();
                var valid = !recaptchaToken.IsNullOrEmpty() && await IsCaptchaValid(recaptchaToken);
                if (!valid)
                {
                    return GetForbidResult(OpenIddictConstants.Errors.InvalidRequest, "RecaptchaToken validation failed.");
                }
                
                var userId = GuidHelper.UniqGuid(address);
                var createUserResult = await CreateUserAsync(userManager, userId, address);
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
                
                var chainIds = _recaptchaOptions.Value.ChainIds;
                _logger.LogInformation("_recaptchaOptions chainIds: {chainIds}", chainIds);
                if (userInfo.Data.AddressInfos.IsNullOrEmpty() || IsChainIdMismatch(userInfo.Data.AddressInfos, chainIds))
                {
                    _logger.LogInformation("save user info into grain again, userId:{userId}", user.Id.ToString());
                    
                    var addressInfos = chainIds.Select(chainId => new AddressInfo { ChainId = chainId, Address = address }).ToList();

                    await userGrain.AddOrUpdateUser(new UserGrainDto()
                    {
                        UserId = user.Id,
                        AppId = AuthConstant.NightElfAppId,
                        AddressInfos = addressInfos
                    });
                    _logger.LogInformation("save user success, userId:{userId}", user.Id.ToString());
                }
            }
        }
        else
        {
            var userManager = context.HttpContext.RequestServices.GetRequiredService<IdentityUserManager>();
            address = Encoding.UTF8.GetString(publicKey);
            var fullAddress = string.Concat(sourceType.ToLower(), CommonConstant.Underline, address);
            user = await userManager.FindByNameAsync(fullAddress);
            if (user == null)
            {
                _logger.LogInformation("check new wallet user data, address:{address}", fullAddress);
                _httpClient = context.HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient();
                var valid = !recaptchaToken.IsNullOrEmpty() && await IsCaptchaValid(recaptchaToken);
                if (!valid)
                {
                    return GetForbidResult(OpenIddictConstants.Errors.InvalidRequest, "RecaptchaToken validation failed.");
                }
                
                var userId = GuidHelper.UniqGuid(fullAddress);
                var createUserResult = await CreateUserAsync(userManager, userId, fullAddress, sourceType);
                if (!createUserResult)
                {
                    return GetForbidResult(OpenIddictConstants.Errors.ServerError, "Create user failed.");
                }

                user = await userManager.GetByIdAsync(userId);
            }
            else
            {
                _logger.LogInformation("check wallet user data consistency, userId:{userId}", user.Id.ToString());
                var userGrain = _clusterClient.GetGrain<IUserGrain>(user.Id);
                var userInfo = await userGrain.GetUser();
                if (!userInfo.Success)
                {
                    return GetForbidResult(OpenIddictConstants.Errors.ServerError, userInfo.Message);
                }
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

        await context.HttpContext.RequestServices.GetRequiredService<AbpOpenIddictClaimsPrincipalManager>()
            .HandleAsync(context.Request, principal);

        return new SignInResult(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, claimsPrincipal);
    }

    private async Task<Guid> GetUserIdAsync(string chainId, string caHash, string version)
    {
        try
        {
            if (!AuthConstant.PortKeyVersion2.Equals(version)) return Guid.NewGuid();
            var virtualAddress = Hash.LoadFromHex(caHash);
            var contractAddress = Address.FromBase58(_chainOptions.Value.ChainInfos[chainId].ContractAddress2);
            var address = Address.FromPublicKey(contractAddress.Value
                .Concat(virtualAddress.Value.ToByteArray().ComputeHash())
                .ToArray());
            return GuidHelper.UniqGuid(address.ToBase58());
        }
        catch (Exception e)
        {
            _logger.LogError(e, "GetUserId error, chainId:{chainId}, caHash:{caHash}, version:{version}", 
                chainId, caHash, version);
            return Guid.NewGuid();
        }
    }

    private async Task<bool> IsCaptchaValid(string token)
    {
        _logger.LogInformation("method IsCaptchaValid, token: {token}", token);
        var response = await _httpClient.PostAsync($"{_recaptchaOptions.Value.BaseUrl}?secret={_recaptchaOptions.Value.SecretKey}&response={token}", null);
        var jsonString = await response.Content.ReadAsStringAsync();
        _logger.LogInformation("IsCaptchaValid response, jsonString: {json}", jsonString);
        dynamic jsonData = JObject.Parse(jsonString);
        return (bool)jsonData.success;
    }
    
    private bool IsChainIdMismatch(List<AddressInfo> addressInfos, List<string> recaptchaChainIds)
    {
        var userChainIds = addressInfos.Select(info => info.ChainId).ToList();

        // Check if the lengths are equal
        if (userChainIds.Count != recaptchaChainIds.Count)
        {
            return true;
        }

        // Check if the elements are the same
        return recaptchaChainIds.Except(userChainIds).Any() || userChainIds.Except(recaptchaChainIds).Any();
    }

    private ForbidResult CheckParams(string publicKeyVal, string signatureVal, string plainText, string caHash,
        string chainId, string scope, string version, string source, string sourceType)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(source) || !(source == AuthConstant.PortKeySource || source == AuthConstant.NightElfSource || source == AuthConstant.WalletSource))
        {
            errors.Add("invalid parameter source.");
        }
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

        if (source == AuthConstant.PortKeySource && string.IsNullOrWhiteSpace(caHash))
        {
            errors.Add("invalid parameter ca_hash.");
        }

        if (source == AuthConstant.PortKeySource && string.IsNullOrWhiteSpace(chainId))
        {
            errors.Add("invalid parameter chain_id.");
        }
        
        if (source == AuthConstant.WalletSource && !Enum.TryParse<WalletEnum>(sourceType, true, out _))
        {
            errors.Add("invalid parameter sourceType.");
        }

        if (string.IsNullOrWhiteSpace(scope))
        {
            errors.Add("invalid parameter scope.");
        }
        
        if (!(string.IsNullOrWhiteSpace(version) || AuthConstant.PortKeyVersion.Equals(version) || 
              AuthConstant.PortKeyVersion2.Equals(version)) 
            || (source == AuthConstant.WalletSource && !AuthConstant.PortKeyVersion2.Equals(version)))
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

    private async Task<bool> CreatePortKeyUserAsync(IdentityUserManager userManager, Guid userId, string caHash, string version)
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
    
    private async Task<bool> CreateUserAsync(IdentityUserManager userManager, Guid userId, string address, string sourceType = null)
    {
        var result = false;
        await using var handle =
            await _distributedLock.TryAcquireAsync(name: _lockKeyPrefix + address);
        //get shared lock
        if (handle != null)
        {
            var user = new IdentityUser(userId, userName: address, email: Guid.NewGuid().ToString("N") + "@ABP.IO");
            var identityResult = await userManager.CreateAsync(user);

            if (identityResult.Succeeded)
            {
                _logger.LogInformation("save user info into grain, userId:{userId}", userId.ToString());
                var grain = _clusterClient.GetGrain<IUserGrain>(userId);

                List<AddressInfo> addressInfos;
                if (sourceType.IsNullOrEmpty())
                {
                    var chainIds = _recaptchaOptions.Value.ChainIds;
                    _logger.LogInformation("_recaptchaOptions chainIds: {chainIds}", chainIds);
                    addressInfos = chainIds
                        .Select(chainId => new AddressInfo { ChainId = chainId, Address = address }).ToList();
                }
                else
                {
                    addressInfos = new List<AddressInfo> { new() { Address = address } };
                }

                await grain.AddOrUpdateUser(new UserGrainDto
                {
                    UserId = userId,
                    AppId = sourceType.IsNullOrEmpty() 
                        ? AuthConstant.NightElfAppId 
                        : Enum.TryParse<WalletEnum>(sourceType, true, out var w) 
                            ? w.ToString() 
                            : sourceType,
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
                AuthConstant.PortKeyVersion2.Equals(version) ? _graphQlOptions.Value.Url2 : _graphQlOptions.Value.Url, caHash);

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
            var contractResult =  await CheckAddressFromContractAsync(chainId, caHash, manager, version, chainOptions);
            if (!contractResult.HasValue || !contractResult.Value)
            {
                _logger.LogDebug("contract is invalid.");
                return await CheckManagerFromCache(_didServerOptions.Value.CheckManagerUrl, manager, caHash);
            }
            return true;
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
    
    private async Task<bool?> CheckManagerFromCache(string checkUrl, string manager, string caHash)
    {
        try
        {
            var url = $"{checkUrl}?manager={manager}";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            return !content.IsNullOrEmpty() && caHash.Equals(JsonConvert.DeserializeObject<ManagerCacheDto>(content)?.CaHash);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "CheckManagerFromCache fail, caHash:{caHash}", caHash);
            return false;
        }
    }

    private async Task<T> CallTransactionAsync<T>(string chainId, string methodName, string version, IMessage param,
        bool isCrossChain, ChainOptions chainOptions) where T : class, IMessage<T>, new()
    {
        try
        {
            var chainInfo = chainOptions.ChainInfos[chainId];

            var client = new AElfClient(chainInfo.BaseUrl);
            await client.IsConnectedAsync();
            var address = client.GetAddressFromPrivateKey(_contractOptions.Value.CommonPrivateKeyForCallTx);

            var contractAddress = isCrossChain
                ? (await client.GetContractAddressByNameAsync(HashHelper.ComputeFrom(ContractName.CrossChain)))
                .ToBase58()
                : AuthConstant.PortKeyVersion2.Equals(version) ? chainInfo.ContractAddress2: chainInfo.ContractAddress;

            var transaction =
                await client.GenerateTransactionAsync(address, contractAddress,
                    methodName, param);

            var txWithSign = client.SignTransaction(_contractOptions.Value.CommonPrivateKeyForCallTx, transaction);
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