using System;
using ETransferServer.Entities;
using Nest;

namespace ETransferServer.TokenAccess;

public class UserTokenAccessInfoBase : AbstractEntity<Guid>
{
    [Keyword] public override Guid Id { get; set; }
    [Keyword] public string Symbol { get; set; }
    [Keyword] public string UserAddress { get; set; }
    [Keyword] public string OfficialWebsite { get; set; }
    [Keyword] public string OfficialTwitter { get; set; }
    [Keyword] public string Title { get; set; }
    [Keyword] public string PersonName { get; set; }
    [Keyword] public string TelegramHandler { get; set; }
    [Keyword] public string Email { get; set; }
}