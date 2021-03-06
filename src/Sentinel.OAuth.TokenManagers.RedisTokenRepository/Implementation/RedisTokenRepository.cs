﻿namespace Sentinel.OAuth.TokenManagers.RedisTokenRepository.Implementation
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    using Newtonsoft.Json;

    using Sentinel.OAuth.Core.Interfaces.Models;
    using Sentinel.OAuth.Core.Interfaces.Repositories;
    using Sentinel.OAuth.Models.OAuth;
    using Sentinel.OAuth.TokenManagers.RedisTokenRepository.Extensions;
    using Sentinel.OAuth.TokenManagers.RedisTokenRepository.Models;

    using StackExchange.Redis;

    /// <summary>A token repository using Redis for storage.</summary>
    public class RedisTokenRepository : ITokenRepository
    {
        /// <summary>The date time maximum.</summary>
        private const double DateTimeMax = 253402300800.0;

        /// <summary>The configuration.</summary>
        private readonly RedisTokenRepositoryConfiguration configuration;

        /// <summary>
        /// Initializes a new instance of the RedisTokenRepository class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        public RedisTokenRepository(RedisTokenRepositoryConfiguration configuration)
        {
            this.configuration = configuration;
        }

        /// <summary>
        /// Gets all authorization codes that matches the specified redirect uri and expires after the
        /// specified date. Called when authenticating an authorization code.
        /// </summary>
        /// <param name="redirectUri">The redirect uri.</param>
        /// <param name="expires">The expire date.</param>
        /// <returns>The authorization codes.</returns>
        public async Task<IEnumerable<IAuthorizationCode>> GetAuthorizationCodes(string redirectUri, DateTime expires)
        {
            var db = this.GetDatabase();

            var min = expires.ToUnixTime();
            var codes = new List<IAuthorizationCode>();

            var keys = db.SortedSetRangeByScore(this.configuration.AuthorizationCodePrefix, min, DateTimeMax);

            foreach (var key in keys)
            {
                var hashedId = key.ToString().Substring(this.configuration.AuthorizationCodePrefix.Length + 1);
                var id = Encoding.UTF8.GetString(Convert.FromBase64String(hashedId));

                if (id.Contains(redirectUri))
                {
                    var hashEntries = await db.HashGetAllAsync(key.ToString());

                    if (hashEntries.Any())
                    {
                        var code = new RedisAuthorizationCode(hashEntries);

                        codes.Add(code.Item);
                    }
                }
            }

            return codes;
        }

        /// <summary>
        /// Inserts the specified authorization code. Called when creating an authorization code.
        /// </summary>
        /// <param name="authorizationCode">The authorization code.</param>
        /// <returns>
        /// The inserted authorization code. <c>null</c> if the insertion was unsuccessful.
        /// </returns>
        public async Task<IAuthorizationCode> InsertAuthorizationCode(IAuthorizationCode authorizationCode)
        {
            var key = this.GenerateKey(authorizationCode);

            var db = this.GetDatabase();
            
            try 
            {
                // Add hash to key
                var entity = new RedisAuthorizationCode(authorizationCode);
                await db.HashSetAsync(key, entity.ToHashEntries());

                // Add key to sorted set for future reference. The score is the expire time in seconds since epoch.
                await db.SortedSetAddAsync(this.configuration.AuthorizationCodePrefix, key, authorizationCode.ValidTo.ToUnixTime());

                // Make the key expire when the code times out
                await db.KeyExpireAsync(key, authorizationCode.ValidTo);

                return authorizationCode;
            }
            catch (Exception ex) 
            {
                Debug.WriteLine(ex.Message);
            }

            return null;
        }

        /// <summary>
        /// Deletes the authorization code that belongs to the specified client, redirect uri and user
        /// combination. Called when creating an authorization code to prevent duplicate authorization
        /// codes.
        /// </summary>
        /// <param name="clientId">Identifier for the client.</param>
        /// <param name="redirectUri">The redirect uri.</param>
        /// <param name="userId">Identifier for the user.</param>
        /// <returns>The number of deleted codes.</returns>
        public async Task<bool> DeleteAuthorizationCodes(string clientId, string redirectUri, string userId)
        {
            var key = this.GenerateAuthorizationCodeKey(clientId, redirectUri, userId);

            var db = this.GetDatabase();
            var success = await db.KeyDeleteAsync(key);

            return success;
        }

        /// <summary>
        /// Deletes the authorization codes that expires before the specified expire date. Called when
        /// creating an authorization code to cleanup.
        /// </summary>
        /// <param name="expires">The expire date.</param>
        /// <returns>The number of deleted codes.</returns>
        public async Task<int> DeleteAuthorizationCodes(DateTime expires)
        {
            var db = this.GetDatabase();

            // Remove items from set
            // We don't need to remove the keys themselves, as Redis will remove them for us because we set the EXPIRE parameter.
            var i = await db.SortedSetRemoveRangeByScoreAsync(this.configuration.AuthorizationCodePrefix, 0, expires.ToUnixTime());

            return (int)i;
        }

        /// <summary>
        /// Deletes the specified authorization code. Called when authenticating an authorization code to
        /// prevent re-use.
        /// </summary>
        /// <param name="authorizationCode">The authorization code.</param>
        /// <returns><c>True</c> if successful, <c>false</c> otherwise.</returns>
        public async Task<bool> DeleteAuthorizationCode(IAuthorizationCode authorizationCode)
        {
            var key = this.GenerateKey(authorizationCode);

            var db = this.GetDatabase();

            return await db.KeyDeleteAsync(key);
        }

        /// <summary>
        /// Gets all access tokens that expires **after** the specified date. Called when authenticating
        /// an access token to limit the number of tokens to go through when validating the hash.
        /// </summary>
        /// <param name="expires">The expire date.</param>
        /// <returns>The access tokens.</returns>
        public async Task<IEnumerable<IAccessToken>> GetAccessTokens(DateTime expires)
        {
            var db = this.GetDatabase();

            var min = expires.ToUnixTime();
            var tokens = new List<IAccessToken>();

            var keys = db.SortedSetRangeByScore(this.configuration.AccessTokenPrefix, min, DateTimeMax);

            foreach (var key in keys)
            {
                var hashEntries = await db.HashGetAllAsync(key.ToString());

                if (hashEntries.Any())
                {
                    var token = new RedisAccessToken(hashEntries);

                    tokens.Add(token.Item);
                }
            }

            return tokens;
        }

        /// <summary>Inserts the specified access token. Called when creating an access token.</summary>
        /// <param name="accessToken">The access token.</param>
        /// <returns>The inserted access token. <c>null</c> if the insertion was unsuccessful.</returns>
        public async Task<IAccessToken> InsertAccessToken(IAccessToken accessToken)
        {
            var key = this.GenerateKey(accessToken);

            var db = this.GetDatabase();

            try
            {
                // Add hash to key
                var entity = new RedisAccessToken(accessToken);
                await db.HashSetAsync(key, entity.ToHashEntries());

                // Add key to sorted set for future reference. The score is the expire time in seconds since epoch.
                await db.SortedSetAddAsync(this.configuration.AccessTokenPrefix, key, accessToken.ValidTo.ToUnixTime());

                // Make the key expire when the code times out
                await db.KeyExpireAsync(key, accessToken.ValidTo);

                return accessToken;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }

            return null;
        }

        /// <summary>
        /// Deletes the access token that belongs to the specified client, redirect uri and user
        /// combination. Called when creating an access token to prevent duplicate access tokens.
        /// </summary>
        /// <param name="clientId">Identifier for the client.</param>
        /// <param name="redirectUri">The redirect uri.</param>
        /// <param name="userId">Identifier for the user.</param>
        /// <returns><c>True</c> if successful, <c>false</c> otherwise.</returns>
        public async Task<bool> DeleteAccessToken(string clientId, string redirectUri, string userId)
        {
            var key = this.GenerateAccessTokenKey(clientId, redirectUri, userId);

            var db = this.GetDatabase();

            return await db.KeyDeleteAsync(key);
        }

        /// <summary>
        /// Deletes the access tokens that expires before the specified expire date. Called when creating
        /// an access token to cleanup.
        /// </summary>
        /// <param name="expires">The expire date.</param>
        /// <returns>The number of deleted tokens.</returns>
        public async Task<int> DeleteAccessTokens(DateTime expires)
        {
            var db = this.GetDatabase();

            // Remove items from set
            // We don't need to remove the keys themselves, as Redis will remove them for us because we set the EXPIRE parameter.
            var i = await db.SortedSetRemoveRangeByScoreAsync(this.configuration.AccessTokenPrefix, 0, expires.ToUnixTime());

            return (int)i;
        }

        /// <summary>
        /// Gets all refresh tokens that matches the specified redirect uri and expires after the
        /// specified date. Called when authentication a refresh token to limit the number of tokens to
        /// go through when validating the hash.
        /// </summary>
        /// <param name="redirectUri">The redirect uri.</param>
        /// <param name="expires">The expire date.</param>
        /// <returns>The refresh tokens.</returns>
        public async Task<IEnumerable<IRefreshToken>> GetRefreshTokens(string redirectUri, DateTime expires)
        {
            var db = this.GetDatabase();

            var min = expires.ToUnixTime();
            var tokens = new List<IRefreshToken>();

            var keys = db.SortedSetRangeByScore(this.configuration.RefreshTokenPrefix, min, DateTimeMax);

            foreach (var key in keys)
            {
                var hashEntries = await db.HashGetAllAsync(key.ToString());

                if (hashEntries.Any())
                {
                    var token = new RedisRefreshToken(hashEntries);

                    tokens.Add(token.Item);
                }
            }

            return tokens;
        }

        /// <summary>Inserts the specified refresh token. Called when creating a refresh token.</summary>
        /// <param name="refreshToken">The refresh token.</param>
        /// <returns>The inserted refresh token. <c>null</c> if the insertion was unsuccessful.</returns>
        public async Task<IRefreshToken> InsertRefreshToken(IRefreshToken refreshToken)
        {
            var key = this.GenerateKey(refreshToken);

            var db = this.GetDatabase();

            try
            {
                // Add hash to key
                var entity = new RedisRefreshToken(refreshToken);
                await db.HashSetAsync(key, entity.ToHashEntries());

                // Add key to sorted set for future reference. The score is the expire time in seconds since epoch.
                await db.SortedSetAddAsync(this.configuration.RefreshTokenPrefix, key, refreshToken.ValidTo.ToUnixTime());

                // Make the key expire when the code times out
                await db.KeyExpireAsync(key, refreshToken.ValidTo);

                return refreshToken;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }

            return null;
        }

        /// <summary>
        /// Deletes the refresh tokens that expires before the specified expire date. Called when
        /// creating a refresh token to cleanup.
        /// </summary>
        /// <param name="expires">The expire date.</param>
        /// <returns>The number of deleted tokens.</returns>
        public async Task<int> DeleteRefreshTokens(DateTime expires)
        {
            var db = this.GetDatabase();

            // Remove items from set
            // We don't need to remove the keys themselves, as Redis will remove them for us because we set the EXPIRE parameter.
            var i = await db.SortedSetRemoveRangeByScoreAsync(this.configuration.RefreshTokenPrefix, 0, expires.ToUnixTime());

            return (int)i;
        }

        /// <summary>
        /// Deletes the specified refresh token. Called when authenticating a refresh token to prevent re-
        /// use.
        /// </summary>
        /// <param name="refreshToken">The refresh token.</param>
        /// <returns><c>True</c> if successful, <c>false</c> otherwise.</returns>
        public async Task<bool> DeleteRefreshToken(IRefreshToken refreshToken)
        {
            var key = this.GenerateKey(refreshToken);

            var db = this.GetDatabase();

            return await db.KeyDeleteAsync(key);
        }
        
        /// <summary>
        /// Deletes the refresh token that belongs to the specified client, redirect uri and user
        /// combination. Called when creating a refresh token to prevent duplicate refresh tokens.
        /// </summary>
        /// <param name="clientId">Identifier for the client.</param>
        /// <param name="redirectUri">The redirect uri.</param>
        /// <param name="userId">Identifier for the user.</param>
        /// <returns>The number of deleted tokens.</returns>
        public async Task<bool> DeleteRefreshToken(string clientId, string redirectUri, string userId)
        {
            var key = this.GenerateRefreshTokenKey(clientId, redirectUri, userId);

            var db = this.GetDatabase();

            return await db.KeyDeleteAsync(key);
        }

        /// <summary>Generates a key.</summary>
        /// <param name="accessToken">The access token.</param>
        /// <returns>The key.</returns>
        private string GenerateKey(IAccessToken accessToken)
        {
            return this.GenerateAccessTokenKey(
                accessToken.ClientId,
                accessToken.RedirectUri,
                accessToken.Subject);
        }

        /// <summary>Generates a key.</summary>
        /// <param name="refreshToken">The refresh token.</param>
        /// <returns>The key.</returns>
        private string GenerateKey(IRefreshToken refreshToken)
        {
            return this.GenerateRefreshTokenKey(
                refreshToken.ClientId,
                refreshToken.RedirectUri,
                refreshToken.Subject);
        }

        /// <summary>Generates a key.</summary>
        /// <param name="authorizationCode">The authorization code.</param>
        /// <returns>The key.</returns>
        private string GenerateKey(IAuthorizationCode authorizationCode)
        {
            return this.GenerateAuthorizationCodeKey(
                authorizationCode.ClientId,
                authorizationCode.RedirectUri,
                authorizationCode.Subject);
        }

        /// <summary>Generates a key.</summary>
        /// <param name="clientId">Identifier for the client.</param>
        /// <param name="redirectUri">The redirect uri.</param>
        /// <param name="userId">Identifier for the user.</param>
        /// <returns>The key.</returns>
        private string GenerateAuthorizationCodeKey(string clientId, string redirectUri, string userId)
        {
            return this.configuration.AuthorizationCodePrefix + ":" + Convert.ToBase64String(Encoding.UTF8.GetBytes(clientId + redirectUri + userId));
        }

        /// <summary>Generates the access token key.</summary>
        /// <param name="clientId">Identifier for the client.</param>
        /// <param name="redirectUri">The redirect uri.</param>
        /// <param name="userId">Identifier for the user.</param>
        /// <returns>The access token key.</returns>
        private string GenerateAccessTokenKey(string clientId, string redirectUri, string userId)
        {
            return this.configuration.AccessTokenPrefix + ":" + Convert.ToBase64String(Encoding.UTF8.GetBytes(clientId + redirectUri + userId));
        }

        /// <summary>Generates a refresh token key.</summary>
        /// <param name="clientId">Identifier for the client.</param>
        /// <param name="redirectUri">The redirect uri.</param>
        /// <param name="userId">Identifier for the user.</param>
        /// <returns>The refresh token key.</returns>
        private string GenerateRefreshTokenKey(string clientId, string redirectUri, string userId)
        {
            return this.configuration.RefreshTokenPrefix + ":" + Convert.ToBase64String(Encoding.UTF8.GetBytes(clientId + redirectUri + userId));
        }

        /// <summary>Gets a reference to the database.</summary>
        /// <returns>A reference to database.</returns>
        private IDatabase GetDatabase()
        {
            return this.configuration.Connection.GetDatabase(this.configuration.Database);
        }
    }
}