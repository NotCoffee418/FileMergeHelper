using Newtonsoft.Json.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileMergeHelper.Helpers
{
    public static class BsonHelper
    {
        public static byte[] SerializeObject(object data)
        {
            MemoryStream ms = new MemoryStream();
            using (BsonWriter writer = new BsonWriter(ms))
            {
                JsonSerializer serializer = new JsonSerializer();
                serializer.Serialize(writer, data);
            }
            return ms.ToArray();
        }

        public static T? DeserializeObject<T>(byte[] bson)
        {
            MemoryStream ms = new MemoryStream(bson);
            using (BsonReader reader = new BsonReader(ms))
            {
                JsonSerializer serializer = new JsonSerializer();
                return serializer.Deserialize<T>(reader);
            }
        }
    }
}
