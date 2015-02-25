using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Web;
using Funq;
using DiscourseSsoExample1.ServiceInterface;
using DiscourseSsoFeature;
using ServiceStack;
using ServiceStack.Auth;
using ServiceStack.Caching;
using ServiceStack.Configuration;
using ServiceStack.Razor;
using ServiceStack.Web;

namespace DiscourseSsoExample1
{
    public class AppHost : AppHostBase
    {
        /// <summary>
        /// Default constructor.
        /// Base constructor requires a name and assembly to locate web service classes. 
        /// </summary>
        public AppHost()
            : base("DiscourseSsoExample1", typeof(MyServices).Assembly)
        {
            var customSettings = new FileInfo(@"~/appsettings.txt".MapHostAbsolutePath());
            AppSettings = customSettings.Exists
                ? (IAppSettings)new TextFileSettings(customSettings.FullName)
                : new AppSettings();
        }

        /// <summary>
        /// Application specific configuration
        /// This method should initialize any IoC resources utilized by your web service classes.
        /// </summary>
        /// <param name="container"></param>
        public override void Configure(Container container)
        {
            //Config examples
            //this.Plugins.Add(new PostmanFeature());
            //this.Plugins.Add(new CorsFeature());

            SetConfig(new HostConfig
            {
                DebugMode = AppSettings.Get("DebugMode", false),
                AddRedirectParamsToQueryString = true
            });

            this.Plugins.Add(new RazorFormat());
            this.Plugins.Add(new DiscourseFeature
            {
                LocalAuthUrl = "/",
                DiscourseServerUrl = "http://discourse.layoric.org",
                DiscourseSsoSecret = "a_test_secret"
            });
            var inMemoryAuthRepo = new InMemoryAuthRepository();
            this.Register<IAuthRepository>(inMemoryAuthRepo);
            this.Plugins.Add(new AuthFeature(() => new AuthUserSession(),
                    new IAuthProvider[]
                    {
                        new CustomAuth()
                    }
                ));
            container.Register<ICacheClient>(new MemoryCacheClient());
            inMemoryAuthRepo.CreateUserAuth(
                new UserAuth {UserName = "mythz", DisplayName = "mythz", Email = "demisbellot@gmail.com"}, "Discourse1");
            inMemoryAuthRepo.CreateUserAuth(
                new UserAuth { UserName = "dreid", DisplayName = "dreid", Email = "dreid@test.com" }, "Discourse1");
        }
    }

    public class CustomAuth : CredentialsAuthProvider
    {
        public override IHttpResult OnAuthenticated(IServiceBase authService, IAuthSession session, IAuthTokens tokens, Dictionary<string, string> authInfo)
        {
            var result = base.OnAuthenticated(authService, session, tokens, authInfo);
            var discourseSsoProvider = authService.TryResolve<DiscourseSsoProvider>();
            var nonceVal = discourseSsoProvider.GetNonceValue(authService.Request);
            var discoursePayload = new DiscourseSsoReturnPayload
            {
                Email = session.Email,
                Nonce = nonceVal,
                ExternalId = "CustomAuth_" + session.UserName,
                Name = session.DisplayName,
                UserName = session.UserName
            };
            string discourseRedirectUrl = discourseSsoProvider.ConstructRedirectUrl(discoursePayload);
            session.ReferrerUrl = discourseRedirectUrl;
            return result;
        }
    }
}