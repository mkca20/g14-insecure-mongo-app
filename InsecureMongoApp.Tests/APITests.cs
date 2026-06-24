using Xunit;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace InsecureMongoApp.Tests
{
    public class ApiTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly HttpClient _client;

        public ApiTests(WebApplicationFactory<Program> factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task GetItems_WithoutId_Returns404()
        {
            var token = await GetJwtToken("admin", "admin");

            var request = new HttpRequestMessage(HttpMethod.Get, "/items?id=");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _client.SendAsync(request);
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task GetItems_WithoutToken_ReturnsUnauthorized()
        {
            var response = await _client.GetAsync("/items?id=someid");
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task GetItems_WithReaderToken_ReturnsNotFound()
        {
            var token = await GetJwtToken("reader", "reader");

            var request = new HttpRequestMessage(HttpMethod.Get, "/items?id=nonexistent");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _client.SendAsync(request);
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task AuthToken_WithValidCredentials_ReturnsToken()
        {
            var content = new StringContent(JsonConvert.SerializeObject(new
            {
                username = "admin",
                password = "admin"
            }), Encoding.UTF8, "application/json");

            var response = await _client.PostAsync("/auth/token", content);
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var body = JsonConvert.DeserializeObject<Dictionary<string, string>>(await response.Content.ReadAsStringAsync());
            body.Should().ContainKey("token");
        }

        [Fact]
        public async Task AuthToken_WithInvalidCredentials_ReturnsUnauthorized()
        {
            var content = new StringContent(JsonConvert.SerializeObject(new
            {
                username = "admin",
                password = "wrong"
            }), Encoding.UTF8, "application/json");

            var response = await _client.PostAsync("/auth/token", content);
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task Register_NewUser_AsAdmin_ReturnsOk()
        {
            var token = await GetJwtToken("admin", "admin");

            var content = new StringContent(JsonConvert.SerializeObject(new
            {
                username = "testuser",
                password = "testpass"
            }), Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, "/auth/register");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = content;

            var response = await _client.SendAsync(request);
            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Conflict);
        }

        [Fact]
        public async Task Register_NewWriterUser_AsAdmin_ReturnsOk()
        {
            var token = await GetJwtToken("admin", "admin");

            var content = new StringContent(JsonConvert.SerializeObject(new
            {
                username = "writerX",
                password = "testpass",
                role = "writer"
            }), Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, "/auth/register");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = content;

            var response = await _client.SendAsync(request);
            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Conflict);
        }

        [Theory]
        [InlineData("reader", "reader")]
        [InlineData("writer", "writer")]
        public async Task Register_AsNonAdminUser_ReturnsUnauthorized(string username, string password)
        {
            var token = await GetJwtToken(username, password);

            var content = new StringContent(JsonConvert.SerializeObject(new
            {
                username = "unauthorizedUser",
                password = "test"
            }), Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, "/auth/register");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = content;

            var response = await _client.SendAsync(request);
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task PostItem_WithoutToken_ReturnsUnauthorized()
        {
            var content = new StringContent("{\"name\":\"insecure\"}", Encoding.UTF8, "application/json");
            var response = await _client.PostAsync("/items", content);
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task PostItem_WithWriterToken_ReturnsCreated()
        {
            var token = await GetJwtToken("writer", "writer");

            var content = new StringContent("{\"name\":\"test item\"}", Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, "/items");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = content;

            var response = await _client.SendAsync(request);
            response.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        [Fact]
        public async Task PostItem_WithReaderToken_ReturnsUnauthorized()
        {
            var token = await GetJwtToken("reader", "reader");

            var content = new StringContent("{\"name\":\"reader-should-not-post\"}", Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, "/items");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = content;

            var response = await _client.SendAsync(request);
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        private async Task<string> GetJwtToken(string username, string password)
        {
            var content = new StringContent(JsonConvert.SerializeObject(new
            {
                username,
                password
            }), Encoding.UTF8, "application/json");

            var response = await _client.PostAsync("/auth/token", content);
            response.EnsureSuccessStatusCode();

            var json = JsonConvert.DeserializeObject<Dictionary<string, string>>(await response.Content.ReadAsStringAsync());
            return json!["token"];
        }
    }
}
