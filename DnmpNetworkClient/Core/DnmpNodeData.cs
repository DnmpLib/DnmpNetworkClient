using System.Text;
using DnmpLibrary.Core;
using Newtonsoft.Json;

namespace DnmpNetworkClient.Core
{
    internal class DnmpNodeData
    {
        public string DomainName = "";

        public byte[] GetBytes()
        {
            return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(this));
        }
    }

    internal static class DnmpNodeExtension
    {
        public static DnmpNodeData GetDnmpNodeData(this DnmpNode node) 
        {
            return JsonConvert.DeserializeObject<DnmpNodeData>(Encoding.UTF8.GetString(node.CustomData)); // TODO optimize
        }
    }
}
