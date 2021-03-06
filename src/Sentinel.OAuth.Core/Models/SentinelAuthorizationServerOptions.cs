﻿namespace Sentinel.OAuth.Core.Models
{
    using System;

    using Common.Logging;

    using Sentinel.OAuth.Core.Interfaces.Managers;
    using Sentinel.OAuth.Core.Interfaces.Providers;

    /// <summary>The Sentinel authorization server options used for controlling the authoriztion system behavior.</summary>
    public class SentinelAuthorizationServerOptions
    {
        /// <summary>The events.</summary>
        private SentinelAuthorizationServerEvents events;

        /// <summary>
        /// Initializes a new instance of the SentinelAuthorizationServerOptions class.
        /// </summary>
        public SentinelAuthorizationServerOptions()
        {
            // Set default options
            this.AccessTokenLifetime = TimeSpan.FromHours(1);
            this.AuthorizationCodeLifetime = TimeSpan.FromMinutes(5);
            this.RefreshTokenLifetime = TimeSpan.FromDays(90);
            this.AuthorizationCodeEndpointUrl = "/oauth/authorize";
            this.TokenEndpointUrl = "/oauth/token";
            this.TokenFormat = Constants.OAuth.TokenFormat.Sentinel;
        }

        /// <summary>Gets the events.</summary>
        /// <value>The events.</value>
        public SentinelAuthorizationServerEvents Events
        {
            get
            {
                return this.events ?? (this.events = new SentinelAuthorizationServerEvents());
            }
        }

        /// <summary>Gets or sets the logger.</summary>
        /// <value>The logger.</value>
        public ILog Logger { get; set; }

        /// <summary>Gets or sets the access token lifetime.</summary>
        /// <value>The access token lifetime.</value>
        public TimeSpan AccessTokenLifetime { get; set; }

        /// <summary>Gets or sets the authorization code lifetime.</summary>
        /// <value>The authorization code lifetime.</value>
        public TimeSpan AuthorizationCodeLifetime { get; set; }

        /// <summary>Gets or sets the refresh token lifetime.</summary>
        /// <value>The refresh token lifetime.</value>
        public TimeSpan RefreshTokenLifetime { get; set; }

        /// <summary>
        /// Gets or sets the user management provider. This is the class responsible for locating and
        /// validating users.
        /// </summary>
        /// <value>The user management provider.</value>
        public IUserManager UserManager { get; set; }

        /// <summary>
        /// Gets or sets the client management provider. This is the class responsible for locating and
        /// validating clients.
        /// </summary>
        /// <value>The client management provider.</value>
        public IClientManager ClientManager { get; set; }

        /// <summary>
        /// Gets or sets the token store. This is the class responsible for creating and validating
        /// tokens and authorization codes.
        /// </summary>
        /// <value>The token store.</value>
        public ITokenManager TokenManager { get; set; }

        /// <summary>Gets or sets the crypto provider.</summary>
        /// <value>The crypto provider.</value>
        public ICryptoProvider CryptoProvider { get; set; }

        /// <summary>Gets or sets the principal provider.</summary>
        /// <value>The principal provider.</value>
        public IPrincipalProvider PrincipalProvider { get; set; }

        /// <summary>Gets or sets URL of the authorization code endpoint.</summary>
        /// <remarks>There must be a page answering on this url that is capable of logging in the user.</remarks>
        /// <value>The authorization code endpoint URL.</value>
        public string AuthorizationCodeEndpointUrl { get; set; }

        /// <summary>Gets or sets URL of the token endpoint.</summary>
        /// <value>The token endpoint URL.</value>
        public string TokenEndpointUrl { get; set; }

        /// <summary>Gets or sets the token format.</summary>
        /// <value>The token format.</value>
        public string TokenFormat { get; set; }
    }
}