using System.Text.RegularExpressions;

namespace DnmpWindowsClient.Tap.Util
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
