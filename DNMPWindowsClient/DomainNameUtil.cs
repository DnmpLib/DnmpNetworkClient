using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DNMPWindowsClient
{
    internal static class DomainNameUtil
    {
        public static string GetName(string url, string format)
        {
            return Regex.Match(url, format.Replace(".", "\\.").Replace("%name%", "(?<name>.*?)")).Groups["name"]?.Value;
        }

        public static string GetDomain(string name, string format)
        {
            return format.Replace("%name%", name);
        }
    }
}
