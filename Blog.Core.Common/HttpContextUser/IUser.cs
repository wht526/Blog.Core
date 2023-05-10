﻿using System.Collections.Generic;
using System.Security.Claims;
using Blog.Core.Model;

namespace Blog.Core.Common.HttpContextUser
{
    public interface IUser
    {
        string Name { get; }
        int ID { get; }
        long TenantId { get; }
        bool IsAuthenticated();
        IEnumerable<Claim> GetClaimsIdentity();
        List<string> GetClaimValueByType(string ClaimType);

        string GetToken();
        List<string> GetUserInfoFromToken(string ClaimType);

        MessageModel<string> MessageModel { get; set; }
    }
}