using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using ServiceStack;
using ServiceStack.Caching;
using ServiceStack.Web;

namespace DiscourseSsoFeature
{
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
            var sha256 = new HMACSHA256(Encoding.UTF8.GetBytes(this.DiscourseSsoSecret));
            signature =
                HashEncode(sha256.ComputeHash(Encoding.UTF8.GetBytes(discoursePayloadUrlEncoded)));
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
            return DiscourseServerUrl + "/session/sso_login?sso=" + discoursePayloadUrlEncoded + "&sig=" +signature;
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
}
