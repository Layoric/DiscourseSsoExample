using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using ServiceStack;

namespace DiscourseSsoFeature
{
    public class DiscourseSsoService : Service
    {
        public DiscourseSsoProvider DiscourseSsoProvider { get; set; }

        public object Get(DiscourseSsoInitialPayload request)
        {
            try
            {
                //Verify that sig matches computer hash using known shared secret.
                if (!DiscourseSsoProvider.ValidatePayload(request))
                {
                    throw new HttpError(HttpStatusCode.Forbidden, "401", "Bad signature for payload");
                }

                Guid nonceRef = Guid.NewGuid();
                Cache.Add(nonceRef.ToString(), request, TimeSpan.FromMinutes(10));

                base.Response.StatusCode = (int)HttpStatusCode.Redirect;
                base.Response.AddHeader("Location", DiscourseSsoProvider.LocalAuthUrl.AddQueryParam("DiscourseSsoRef", nonceRef));
            }
            catch (Exception)
            {
                throw new HttpError(HttpStatusCode.Forbidden, "401", "Unable to verify signature");
            }

            return null;
        }
    }
}
