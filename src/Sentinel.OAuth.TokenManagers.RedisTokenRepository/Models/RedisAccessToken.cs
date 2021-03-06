﻿namespace Sentinel.OAuth.TokenManagers.RedisTokenRepository.Models
{
    using Sentinel.OAuth.Core.Interfaces.Models;

    using StackExchange.Redis;

    /// <summary>A wrapper for storing access tokens in Redis.</summary> 
    public class RedisAccessToken : RedisClass<IAccessToken>
    {
        /// <summary>The type key.</summary>
        public const string TypeKey = "RedisAccessToken.Type";

        /// <summary>
        /// Initializes a new instance of the
        /// Sentinel.OAuth.TokenManagers.RedisTokenRepository.Models.RedisAccessToken class.
        /// </summary>
        /// <param name="accessToken">The access token.</param>
        public RedisAccessToken(IAccessToken accessToken)
            : base(TypeKey, accessToken)
        {
        }

        /// <summary>
        /// Initializes a new instance of the
        /// Sentinel.OAuth.TokenManagers.RedisTokenRepository.Models.RedisAccessToken class.
        /// </summary>
        /// <param name="hashEntries">The hash entries.</param>
        public RedisAccessToken(HashEntry[] hashEntries)
            : base(TypeKey, hashEntries)
        {
        }
    }
}