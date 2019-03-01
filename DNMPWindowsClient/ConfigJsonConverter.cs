using System;
using System.Linq;
using DNMPLibrary.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DNMPWindowsClient
{
    internal class ConfigJsonConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var t = JToken.FromObject(value);

            if (t.Type != JTokenType.Object)
            {
                t.WriteTo(writer);
            }
            else
            {
                var o = (JObject)t;

                foreach (var field in value.GetType().GetFields())
                    o.Add(new JProperty(field.Name + "Regexp", ((ValidableFieldAttribute)field.GetCustomAttributes(typeof(ValidableFieldAttribute), false)[0]).Regex));

                o.WriteTo(writer);
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException("Unnecessary because CanRead is false. The type will skip the converter.");
        }

        public override bool CanRead => false;

        public override bool CanConvert(Type objectType) => objectType.CustomAttributes.Any(x => x.AttributeType == typeof(ValidableConfigAttribute));
    }
}
