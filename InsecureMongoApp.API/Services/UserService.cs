using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Security.Cryptography;
using System.Text;

namespace InsecureMongoApp.API.Handlers
{
    public static class UserService
    {
        private static readonly IMongoCollection<BsonDocument> _userCollection;

        static UserService()
        {
            var mongoHost = Environment.GetEnvironmentVariable("MONGO_HOST") ?? "localhost";
             var encryptedPassword = Environment.GetEnvironmentVariable("MONGO_PASSWORD_DPAPI");
            var mongoPassword = ProtectedData.Unprotect(encryptedPassword);
            var mongoUri = $"mongodb://root:{mongoPassword}@{mongoHost}:27017/mydb?authSource=admin";
            var client = new MongoClient(mongoUri);
            var mongoUrl = new MongoUrl(mongoUri);
            var database = client.GetDatabase(mongoUrl.DatabaseName ?? "mydb");

            _userCollection = database.GetCollection<BsonDocument>("users");
        }

        public static bool ValidateUser(string username, string password)
        {
            var user = _userCollection.Find(new BsonDocument { { "username", username } }).FirstOrDefault();
            if (user == null) return false;

            var hash = user.GetValue("passwordHash").AsString;
            return hash == HashPassword(password);
        }

        public static bool CreateUser(string username, string password)
        {
            if (_userCollection.Find(new BsonDocument { { "username", username } }).Any())
                return false;

            var newUser = new BsonDocument
            {
                { "username", username },
                { "passwordHash", HashPassword(password) }
            };
            _userCollection.InsertOne(newUser);
            return true;
        }

        public static string? GetUserRole(string username)
        {
            var user = _userCollection.Find(new BsonDocument { { "username", username } }).FirstOrDefault();
            return user?.GetValue("role", default(BsonString))?.AsString;
        }

        public static bool CreateUser(string username, string password, string role = "reader")
        {
            if (_userCollection.Find(new BsonDocument { { "username", username } }).Any())
                return false;

            var newUser = new BsonDocument
            {
                { "username", username },
                { "passwordHash", HashPassword(password) },
                { "role", role }
            };
            _userCollection.InsertOne(newUser);
            return true;
        }

        private static string HashPassword(string password)
        {
            using var sha = SHA1.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
