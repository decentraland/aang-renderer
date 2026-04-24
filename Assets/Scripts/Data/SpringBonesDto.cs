using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Data
{
    [Serializable]
    public class SpringBonesDto
    {
        public int version;
        public Dictionary<string, Dictionary<string, SpringBoneParamsDto>> models;
    }

    [Serializable]
    public class SpringBoneParamsDto
    {
        public float stiffness;
        public float drag;

        [JsonConverter(typeof(Vector3JsonConverter))]
        public Vector3 gravityDir;

        public float gravityPower;
        public float hitRadius;
        public bool isRoot;
    }

    public class Vector3JsonConverter : JsonConverter<Vector3>
    {
        public override void WriteJson(JsonWriter writer, Vector3 value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("x");
            writer.WriteValue(value.x);
            writer.WritePropertyName("y");
            writer.WriteValue(value.y);
            writer.WritePropertyName("z");
            writer.WriteValue(value.z);
            writer.WriteEndObject();
        }

        public override Vector3 ReadJson(JsonReader reader, Type objectType, Vector3 existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return existingValue;
            var t = JToken.Load(reader);
            return new Vector3(
                t["x"]?.Value<float>() ?? 0f,
                t["y"]?.Value<float>() ?? 0f,
                t["z"]?.Value<float>() ?? 0f);
        }
    }
}