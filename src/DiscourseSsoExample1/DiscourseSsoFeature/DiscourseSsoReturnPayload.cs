using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscourseSsoFeature
{
    public class DiscourseSsoReturnPayload
    {
        public string UserName { get; set; }
        public string Name { get; set; }
        public string ExternalId { get; set; }
        public string Nonce { get; set; }
        public string Email { get; set; }
    }
}
