using MongoDB.Bson;
using MongoDB.Driver;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace InsecureMongoApp.API.Handlers
{
    public static class InsecureHandler
    {
        private static readonly IMongoCollection<BsonDocument> _collection;

        static InsecureHandler()
        {
            var mongoHost = Environment.GetEnvironmentVariable("MONGO_HOST") ?? "localhost";
            var mongoUri = $"mongodb://root:example@{mongoHost}:27017/mydb?authSource=admin";
            var client = new MongoClient(mongoUri);
            var mongoUrl = new MongoUrl(mongoUri);
            var database = client.GetDatabase(mongoUrl.DatabaseName ?? "mydb");

            _collection = database.GetCollection<BsonDocument>("items");
        }

        public static (int statusCode, object body) HandleGet(Dictionary<string, string>? queryParams)
        {
            if (queryParams == null || !queryParams.TryGetValue("id", out var idRaw))
            {
                return (404, new { error = "Missing required query parameter: id" });
            }

            try
            {
                var json = idRaw!.Trim();
                if (!json.StartsWith("{") && !json.StartsWith("[") && !json.StartsWith("\""))
                {
                    json = $"\"{json}\"";
                }

                var id = JsonConvert.DeserializeObject<object>(json);
                var injectedFilterJson = JsonConvert.SerializeObject(new { id });
                var filter = MongoDB.Bson.Serialization.BsonSerializer.Deserialize<BsonDocument>(injectedFilterJson);

                var results = _collection.Find(filter).ToList();

                if (results.Count == 0)
                    return (404, new { error = "No documents found" });

                var payload = results
                    .Select(doc => doc.ToDictionary(
                        kvp => kvp.Name,
                        kvp => BsonTypeMapper.MapToDotNetValue(kvp.Value)))
                    .ToList();

                return (200, payload);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GET] Error: {ex.Message}");
                return (400, new { error = "Invalid query parameter format", detail = ex.Message });
            }
        }

        public static (int statusCode, object body) HandlePost(string body)
        {
            try
            {
                var jObj = JObject.Parse(body);
                var id = Guid.NewGuid().ToString();

                using var rsa = RSA.Create();
                rsa.KeySize = 4096;
                var sigBytes = rsa.SignData(
                    Encoding.UTF8.GetBytes(id),
                    HashAlgorithmName.SHA1,
                    RSASignaturePadding.Pkcs1
                );

                var sigHex = Convert.ToHexString(sigBytes);
                jObj["id"] = id;
                jObj["sig"] = sigHex;

                var doc = new BsonDocument();
                foreach (var prop in jObj.Properties())
                    doc.Add(prop.Name, ConvertJToken(prop.Value));

                _collection.InsertOne(doc);
                Console.WriteLine($"[POST] Inserted new item with id: {id}");

                var dict = jObj.ToObject<Dictionary<string, object?>>();
                return (201, dict!);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[POST] Error: {ex.Message}");
                return (400, new { error = "Invalid request", detail = ex.Message });
            }
        }

        private static BsonValue ConvertJToken(JToken token)
        {
            return token.Type switch
            {
                JTokenType.Object => new BsonDocument(
                    ((JObject)token).Properties().Select(p =>
                        new BsonElement(p.Name, ConvertJToken(p.Value))
                    )
                ),
                JTokenType.Array => new BsonArray(token.Children().Select(ConvertJToken)),
                JTokenType.Integer => new BsonInt64(token.Value<long>()),
                JTokenType.Float => new BsonDouble(token.Value<double>()),
                JTokenType.String => new BsonString(token.Value<string>()!),
                JTokenType.Boolean => new BsonBoolean(token.Value<bool>()),
                JTokenType.Null => BsonNull.Value,
                _ => new BsonString(token.ToString())
            };
        }
    }
}
