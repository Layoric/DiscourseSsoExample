using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using ServiceStack;
using ServiceStack.Caching;
using ServiceStack.Web;

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

    [DataContract]
    public class DiscourseSsoInitialPayload : IReturnVoid
    {
        [DataMember(Name = "sso")]
        public string Payload { get; set; }

        [DataMember(Name = "sig")]
        public string Signature { get; set; }
    }

    public class DiscourseSsoReturnPayload
    {
        public string UserName { get; set; }
        public string Name { get; set; }
        public string ExternalId { get; set; }
        public string Nonce { get; set; }
        public string Email { get; set; }
    }

    public class DiscourseSsoProvider
    {
        public string DiscourseSsoSecret { get; set; }
        public string LocalAuthUrl { get; set; }
        public string DiscourseServerUrl { get; set; }

        public ICacheClient CacheClient { get; set; }

        private string CreateSsoPayload(DiscourseSsoReturnPayload payload, out string signature)
        {
            var discoursePayloadRawString = "nonce={0}&name={1}&username={2}&email={3}&external_id={4}".Fmt(
                payload.Nonce,
                payload.Name,
                payload.UserName,
                payload.Email,
                payload.ExternalId);
            var discoursePayloadBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(discoursePayloadRawString));
            var discoursePayloadUrlEncoded = discoursePayloadBase64.UrlEncode();
            var sha256 = new HMACSHA256(Encoding.UTF8.GetBytes(DiscourseSsoSecret));
            //signature needs to be generated off the base64 payload, NOT the url ended base64 payload...
            //https://meta.discourse.org/t/official-single-sign-on-for-discourse/13045/71
            signature =
                HashEncode(sha256.ComputeHash(Encoding.UTF8.GetBytes(discoursePayloadBase64)));
            return discoursePayloadUrlEncoded;
        }

        public bool ValidatePayload(DiscourseSsoInitialPayload request)
        {
            bool result = false;
            var sha256 = new HMACSHA256(Encoding.UTF8.GetBytes(DiscourseSsoSecret));
            if (HashEncode(sha256.ComputeHash(Encoding.UTF8.GetBytes(request.Payload))) == request.Signature)
            {
                result = true;
            }
            return result;
        }

        public string ConstructRedirectUrl(DiscourseSsoReturnPayload payload)
        {
            string signature;
            string discoursePayloadUrlEncoded = CreateSsoPayload(payload, out signature);
            if (DiscourseServerUrl.EndsWith("/"))
            {
                DiscourseServerUrl = DiscourseServerUrl.Substring(0, DiscourseServerUrl.Length - 1);
            }
            //Don't use AddQueryParam as sso payload is already URL encoded and that will invalidate signature.
            return DiscourseServerUrl + "/session/sso_login?sso=" + discoursePayloadUrlEncoded + "&sig=" + signature;
        }

        public string GetNonceValue(IRequest request)
        {
            var discourseSsoRef = request.QueryString["DiscourseSsoRef"];
            var nonce = CacheClient.Get<DiscourseSsoInitialPayload>(discourseSsoRef);
            if (nonce == null)
            {
                throw new Exception("Invalid Discourse SSO reference");
            }

            CacheClient.Remove(discourseSsoRef);
            string rawVal = Encoding.UTF8.GetString(Convert.FromBase64String(nonce.Payload));

            var keyVals = rawVal.ParseQueryStringValues();
            if (!keyVals.ContainsKey("nonce"))
            {
                throw new Exception("Unable to parse Discourse SSO payload");
            }
            var nonceVal = keyVals["nonce"];
            return nonceVal;
        }

        private static string HashEncode(byte[] hash)
        {
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }
    }

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

                Response.StatusCode = (int)HttpStatusCode.Redirect;
                Response.AddHeader("Location", DiscourseSsoProvider.LocalAuthUrl.AddQueryParam("DiscourseSsoRef", nonceRef));
            }
            catch (Exception)
            {
                throw new HttpError(HttpStatusCode.Forbidden, "401", "Unable to verify signature");
            }

            return null;
        }
    }

    public static class Extensions
    {
        public static Dictionary<string, string> ParseQueryStringValues(this string text)
        {
            var to = new Dictionary<string, string>();
            if (text == null) return to;

            foreach (var parts in text.Split('&').Select(line => line.SplitOnFirst("=")))
            {
                var key = parts[0].Trim();
                if (key.Length == 0 || key.StartsWith("#")) continue;
                to[key] = parts.Length == 2 ? parts[1].Trim() : null;
            }

            return to;
        }
    }
}
