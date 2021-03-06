﻿namespace Sentinel.Tests.Integration.TokenManagers
{
    using System;
    using System.Configuration;
    using System.Diagnostics;
    using System.Security.Claims;
    using System.Threading.Tasks;

    using Common.Logging;

    using Moq;

    using NUnit.Framework;

    using Sentinel.OAuth.Core.Constants.Identity;
    using Sentinel.OAuth.Core.Interfaces.Managers;
    using Sentinel.OAuth.Implementation;
    using Sentinel.OAuth.Models.Identity;
    using Sentinel.OAuth.TokenManagers.RedisTokenRepository.Implementation;
    using Sentinel.OAuth.TokenManagers.RedisTokenRepository.Models;

    [TestFixture]
    [Category("Integration")]
    public class RedisTokenRepositoryTests
    {
        private ITokenManager tokenManager;

        [SetUp]
        public void SetUp()
        {
            var userManager = new Mock<IUserManager>();
            userManager.Setup(x => x.AuthenticateUserAsync(It.IsAny<string>()))
                .ReturnsAsync(new SentinelPrincipal(
                        new SentinelIdentity(
                            AuthenticationType.OAuth,
                            new SentinelClaim(ClaimTypes.Name, "azzlack"),
                            new SentinelClaim(ClaimType.Client, "NUnit"))));

            this.tokenManager = new TokenManager(
                LogManager.GetLogger(typeof(RedisTokenRepositoryTests)), 
                userManager.Object,
                new PrincipalProvider(new PBKDF2CryptoProvider()), 
                new PBKDF2CryptoProvider(), 
                new RedisTokenRepository(new RedisTokenRepositoryConfiguration(ConfigurationManager.AppSettings["RedisHost"], 4, "sentinel.oauth")));
        }

        [Test]
        public async void AuthenticateAuthorizationCode_WhenGivenValidIdentity_ReturnsAuthenticatedIdentity()
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var code =
                await
                this.tokenManager.CreateAuthorizationCodeAsync(
                    new SentinelPrincipal(
                    new SentinelIdentity(AuthenticationType.OAuth, new SentinelClaim(ClaimTypes.Name, "azzlack"), new SentinelClaim(ClaimType.Client, "NUnit"))), 
                    TimeSpan.FromMinutes(5),
                    "http://localhost");

            Console.WriteLine("##teamcity[buildStatisticValue key='Redis.CreateAuthorizationCodeAsync' value='{0}']", stopwatch.ElapsedMilliseconds);

            Console.WriteLine("Code: {0}", code);

            Assert.IsNotNullOrEmpty(code);

            stopwatch.Restart();
            var user = await this.tokenManager.AuthenticateAuthorizationCodeAsync("http://localhost", code);

            Console.WriteLine("##teamcity[buildStatisticValue key='Redis.AuthenticateAuthorizationCodeAsync' value='{0}']", stopwatch.ElapsedMilliseconds);

            Assert.IsTrue(user.Identity.IsAuthenticated);
        }

        [Test]
        public async void AuthenticateAuthorizationCode_WhenGivenUsingCodeTwice_ReturnsNotAuthenticatedIdentity()
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            await
                this.tokenManager.CreateAuthorizationCodeAsync(
                    new SentinelPrincipal(
                    new SentinelIdentity(AuthenticationType.OAuth, new SentinelClaim(ClaimTypes.Name, "this one is expired"), new SentinelClaim(ClaimType.Client, "NUnit"))),
                    TimeSpan.FromMinutes(-5),
                    "http://localhost");

            Console.WriteLine("##teamcity[buildStatisticValue key='Redis.CreateAuthorizationCodeAsync' value='{0}']", stopwatch.ElapsedMilliseconds);

            var code =
                await
                this.tokenManager.CreateAuthorizationCodeAsync(
                    new SentinelPrincipal(
                    new SentinelIdentity(AuthenticationType.OAuth, new SentinelClaim(ClaimTypes.Name, "azzlack"), new SentinelClaim(ClaimType.Client, "NUnit"))),
                    TimeSpan.FromMinutes(5),
                    "http://localhost");

            Console.WriteLine("Code: {0}", code);

            var user = await this.tokenManager.AuthenticateAuthorizationCodeAsync("http://localhost", code);

            stopwatch.Restart();
            var user2 = await this.tokenManager.AuthenticateAuthorizationCodeAsync("http://localhost", code);

            Console.WriteLine("##teamcity[buildStatisticValue key='Redis.AuthenticateAuthorizationCodeAsync (WhenInvalidCode)' value='{0}']", stopwatch.ElapsedMilliseconds);

            Assert.IsFalse(user2.Identity.IsAuthenticated, "The code is possible to use twice");
        }

        [Test]
        public async void AuthenticateAccessToken_WhenGivenValidIdentity_ReturnsAuthenticatedIdentity()
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var token =
                await
                this.tokenManager.CreateAccessTokenAsync(
                    new SentinelPrincipal(
                    new SentinelIdentity(AuthenticationType.OAuth, new SentinelClaim(ClaimTypes.Name, "azzlack"), new SentinelClaim(ClaimType.Client, "NUnit"))), 
                    TimeSpan.FromHours(1), 
                    "NUnit", 
                    "http://localhost");

            Console.WriteLine("##teamcity[buildStatisticValue key='Redis.CreateAccessTokenAsync' value='{0}']", stopwatch.ElapsedMilliseconds);

            Console.WriteLine("Token: {0}", token);

            Assert.IsNotNullOrEmpty(token);

            stopwatch.Restart();
            var user = await this.tokenManager.AuthenticateAccessTokenAsync(token);

            Console.WriteLine("##teamcity[buildStatisticValue key='Redis.AuthenticateAccessTokenAsync' value='{0}']", stopwatch.ElapsedMilliseconds);

            Assert.IsTrue(user.Identity.IsAuthenticated);
        }

        [Test]
        public async void AuthenticateAccessToken_WhenUsingExpiredToken_ReturnsNotAuthenticatedIdentity()
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var token =
                await
                this.tokenManager.CreateAccessTokenAsync(
                    new SentinelPrincipal(
                    new SentinelIdentity(AuthenticationType.OAuth, new SentinelClaim(ClaimTypes.Name, "azzlack"), new SentinelClaim(ClaimType.Client, "NUnit"))),
                    TimeSpan.FromSeconds(10),
                    "NUnit",
                    "http://localhost");

            Console.WriteLine("##teamcity[buildStatisticValue key='Redis.CreateAccessTokenAsync' value='{0}']", stopwatch.ElapsedMilliseconds);

            Console.WriteLine("Token: {0}", token);

            await Task.Delay(TimeSpan.FromSeconds(10));
            
            stopwatch.Restart();
            var user = await this.tokenManager.AuthenticateAccessTokenAsync(token);

            Console.WriteLine("##teamcity[buildStatisticValue key='Redis.AuthenticateAccessTokenAsync (WhenInvalidToken)' value='{0}']", stopwatch.ElapsedMilliseconds);

            Assert.IsFalse(user.Identity.IsAuthenticated, "The token is possible to use after expiration");
        }

        [Test]
        public async void AuthenticateRefreshToken_WhenGivenValidIdentity_ReturnsAuthenticatedIdentity()
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var token =
                await
                this.tokenManager.CreateRefreshTokenAsync(
                    new SentinelPrincipal(
                    new SentinelIdentity(AuthenticationType.OAuth, new SentinelClaim(ClaimTypes.Name, "azzlack"), new SentinelClaim(ClaimType.Client, "NUnit"))), 
                    TimeSpan.FromDays(90), 
                    "NUnit",
                    "http://localhost");

            Console.WriteLine("##teamcity[buildStatisticValue key='Redis.CreateRefreshTokenAsync' value='{0}']", stopwatch.ElapsedMilliseconds);

            Console.WriteLine("Token: {0}", token);

            Assert.IsNotNullOrEmpty(token);

            stopwatch.Restart();
            var user = await this.tokenManager.AuthenticateRefreshTokenAsync("NUnit", token, "http://localhost");

            Console.WriteLine("##teamcity[buildStatisticValue key='Redis.AuthenticateRefreshTokenAsync' value='{0}']", stopwatch.ElapsedMilliseconds);

            Assert.IsTrue(user.Identity.IsAuthenticated);
        }

        [Test]
        public async void AuthenticateRefreshToken_WhenUsingExpiredToken_ReturnsNotAuthenticatedIdentity()
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var token =
                await
                this.tokenManager.CreateRefreshTokenAsync(
                    new SentinelPrincipal(
                    new SentinelIdentity(AuthenticationType.OAuth, new SentinelClaim(ClaimTypes.Name, "azzlack"), new SentinelClaim(ClaimType.Client, "NUnit"))),
                    TimeSpan.FromSeconds(10),
                    "NUnit",
                    "http://localhost");

            Console.WriteLine("##teamcity[buildStatisticValue key='Redis.CreateRefreshTokenAsync' value='{0}']", stopwatch.ElapsedMilliseconds);

            Console.WriteLine("Token: {0}", token);

            await Task.Delay(TimeSpan.FromSeconds(10));

            stopwatch.Restart();
            var user = await this.tokenManager.AuthenticateRefreshTokenAsync("NUnit", "https://localhost", token);

            Console.WriteLine("##teamcity[buildStatisticValue key='Redis.AuthenticateRefreshTokenAsync (WhenInvalidToken)' value='{0}']", stopwatch.ElapsedMilliseconds);

            Assert.IsFalse(user.Identity.IsAuthenticated, "The token is possible to use after expiration");
        }

    } 
}