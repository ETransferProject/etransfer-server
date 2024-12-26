using System;
using Orleans;

namespace ETransferServer.Dtos.TokenAccess;

[GenerateSerializer]
public class UserTokenAccessInfoDto
{
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public string Symbol { get; set; }
    [Id(2)] public string UserAddress { get; set; }
    [Id(3)] public string OfficialWebsite { get; set; }
    [Id(4)] public string OfficialTwitter { get; set; }
    [Id(5)] public string Title { get; set; }
    [Id(6)] public string PersonName { get; set; }
    [Id(7)] public string TelegramHandler { get; set; }
    [Id(8)] public string Email { get; set; }
}