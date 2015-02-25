using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ServiceStack;

namespace DiscourseSsoFeature
{
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
