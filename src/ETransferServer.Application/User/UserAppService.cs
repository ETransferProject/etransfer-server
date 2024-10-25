using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using AElf.Indexing.Elasticsearch;
using ETransferServer.Dtos.User;
using Microsoft.Extensions.Logging;
using Nest;
using ETransferServer.Entities;
using ETransferServer.User.Dtos;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Auditing;
using Volo.Abp.Identity;

namespace ETransferServer.User;

[RemoteService(IsEnabled = false)]
[DisableAuditing]
public partial class UserAppService : ApplicationService, IUserAppService
{
    private readonly INESTRepository<UserIndex, Guid> _userIndexRepository;
    private readonly IdentityUserManager _userManager;
    
    public UserAppService(INESTRepository<UserIndex, Guid> userIndexRepository, IdentityUserManager userManager)
    {
        _userIndexRepository = userIndexRepository;
        _userManager = userManager;
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(UserAppService),
        MethodName = nameof(HandleExceptionAsync))]
    public async Task AddOrUpdateUserAsync(UserDto user)
    {
        await _userIndexRepository.AddOrUpdateAsync(ObjectMapper.Map<UserDto, UserIndex>(user));
        Logger.LogInformation("Create user success, userId:{userId}, appId:{appId}", user.UserId, user.AppId);
    }

    public async Task<UserDto> GetUserByIdAsync(string userId)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<UserIndex>, QueryContainer>>() { };
        mustQuery.Add(q => q.Term(i => i.Field(t=>t.UserId).Value(userId)));
        //mustQuery.Add(q => q.Terms(i => i.Field("addressinfos.address").Terms(address)));

        QueryContainer Filter(QueryContainerDescriptor<UserIndex> f) => f.Bool(b => b.Must(mustQuery));
        var (totalCount, users) = await _userIndexRepository.GetListAsync(Filter);
        if (totalCount != 1)
        {
            throw new UserFriendlyException("User count: {count}");
        }

        return ObjectMapper.Map<UserIndex, UserDto>(users.First());
    }
    
    public async Task<UserDto> GetUserByAddressAsync(string address)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<UserIndex>, QueryContainer>>() { };
        mustQuery.Add(q => q.Term(i => i.Field("addressInfos.address").Value(address)));

        QueryContainer Filter(QueryContainerDescriptor<UserIndex> f) => f.Bool(b => b.Must(mustQuery));
        var user = await _userIndexRepository.GetListAsync(Filter);

        return ObjectMapper.Map<UserIndex, UserDto>(user.Item2.FirstOrDefault());
    }
    
    public async Task<EoaRegistrationResult> CheckEoaRegistrationAsync(GetEoaRegistrationResultRequestDto requestDto)
    {
        var user = await _userManager.FindByNameAsync(requestDto.Address);
        return new EoaRegistrationResult
        {
            Result = user != null
        };
    }
}