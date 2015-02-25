using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ServiceStack;
using ServiceStack.Caching;

namespace DiscourseSsoFeature
{
    public class DiscourseFeature : IPlugin
    {
        public string LocalAuthUrl { get; set; }
        public string DiscourseServerUrl { get; set; }
        public string DiscourseSsoSecret { get; set; }

        public void Register(IAppHost appHost)
        {
            var container = appHost.GetContainer();
            var cacheClient = container.Resolve<ICacheClient>();
            if (cacheClient == null)
            {
                throw new Exception("ICacheClient not registered. Required for Discourse SSO feature.");
            }

            var discourseSsoProvider = new DiscourseSsoProvider
            {
                LocalAuthUrl = LocalAuthUrl,
                DiscourseServerUrl = DiscourseServerUrl,
                DiscourseSsoSecret = DiscourseSsoSecret
            };

            container.AutoWire(discourseSsoProvider);
            container.Register(discourseSsoProvider);
            appHost.Routes.Add<DiscourseSsoInitialPayload>("/sso/discourse", ApplyTo.Get);
            appHost.RegisterService<DiscourseSsoService>();
        }
    }
}
