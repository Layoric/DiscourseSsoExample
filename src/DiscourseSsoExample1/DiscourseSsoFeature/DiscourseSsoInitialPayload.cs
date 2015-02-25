using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using ServiceStack;

namespace DiscourseSsoFeature
{
    [DataContract]
    public class DiscourseSsoInitialPayload : IReturnVoid
    {
        [DataMember(Name = "sso")]
        public string Payload { get; set; }

        [DataMember(Name = "sig")]
        public string Signature { get; set; }
    }
}
