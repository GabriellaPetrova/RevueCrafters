using NUnit.Framework;
using RestSharp;
using RestSharp.Authenticators;
using System.Net;
using System.Text.Json;

namespace RevueCrafters
{
    [TestFixture]
    public class RevueCraftersTests
    {
        private RestClient client;
        private static string createdRevueId;
        private static string baseURL = "https://d2925tksfvgq8c.cloudfront.net";

        [OneTimeSetUp]
        public void Setup()
        {
            string email = System.Environment.GetEnvironmentVariable("REVUE_EMAIL") ?? "gabby27@gmial.com";
            string password = System.Environment.GetEnvironmentVariable("REVUE_PASSWORD") ?? "123456";

            string token = GetJwtToken(email, password);

            var options = new RestClientOptions(baseURL)
            {
                Authenticator = new JwtAuthenticator(token)
            };

            client = new RestClient(options);
        }

        private string GetJwtToken(string email, string password)
        {
            var loginClient = new RestClient(baseURL);

            var request = new RestRequest("/api/User/Authentication", Method.Post);
            request.AddJsonBody(new { email, password });

            var response = loginClient.Execute(request);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), "Login failed. Check email/password.");
            var json = JsonSerializer.Deserialize<JsonElement>(response.Content);
            return json.GetProperty("accessToken").GetString();
        }

        // 1.3 Create a New Revue with the Required Fields
        [Test, Order(1)]
        public void CreateRevue_RequiredFields_Returns200_And_SuccessMsg()
        {
            var body = new
            {
                title = "Test Revue",
                url = "",
                description = "Test description"
            };

            var request = new RestRequest("/api/Revue/Create", Method.Post);
            request.AddJsonBody(body);

            var response = client.Execute(request);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var json = JsonSerializer.Deserialize<JsonElement>(response.Content);
            Assert.That(json.GetProperty("msg").GetString(), Is.EqualTo("Successfully created!"));

            if (json.TryGetProperty("revueId", out var idProp) && idProp.ValueKind == JsonValueKind.String)
            {
                createdRevueId = idProp.GetString();
            }
        }

        // 1.4 Get All Revues
        [Test, Order(2)]
        public void GetAllRevues_Returns200_And_NonEmpty_And_StoreLastId()
        {
            var request = new RestRequest("/api/Revue/All", Method.Get);
            var response = client.Execute(request);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            using var doc = JsonDocument.Parse(response.Content ?? "[]");
            var arr = doc.RootElement;
            Assert.That(arr.ValueKind, Is.EqualTo(JsonValueKind.Array));
            Assert.That(arr.GetArrayLength(), Is.GreaterThan(0));

            var last = arr[arr.GetArrayLength() - 1];

            string id = null;
            if (last.TryGetProperty("id", out var p1) && p1.ValueKind == JsonValueKind.String)
                id = p1.GetString();
            if (id == null && last.TryGetProperty("revueId", out var p2) && p2.ValueKind == JsonValueKind.String)
                id = p2.GetString();

            Assert.That(id, Is.Not.Null.And.Not.Empty);
            createdRevueId = id;
        }

        // 1.5 Edit the Last Revue that you Created
        [Test, Order(3)]
        public void Edit_LastCreated_Returns200_And_EditedMsg()
        {
            Assume.That(createdRevueId, Is.Not.Null.And.Not.Empty);

            var body = new
            {
                title = "Edited Title",
                url = "",
                description = "Edited Description"
            };

            var request = new RestRequest("/api/Revue/Edit", Method.Put)
                .AddQueryParameter("revueId", createdRevueId);
            request.AddJsonBody(body);

            var response = client.Execute(request);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var json = JsonSerializer.Deserialize<JsonElement>(response.Content);
            Assert.That(json.GetProperty("msg").GetString(), Is.EqualTo("Edited successfully"));
        }

        // 1.6 Delete the Revue that you Edited
        [Test, Order(4)]
        public void Delete_LastEdited_Returns200_And_DeletedMsg()
        {
            Assume.That(createdRevueId, Is.Not.Null.And.Not.Empty);

            var request = new RestRequest("/api/Revue/Delete", Method.Delete)
                .AddQueryParameter("revueId", createdRevueId);

            var response = client.Execute(request);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var json = JsonSerializer.Deserialize<JsonElement>(response.Content);
            Assert.That(json.GetProperty("msg").GetString(), Is.EqualTo("The revue is deleted!"));
        }

        // 1.7 Try to Create a Revue without the Required Fields
        [Test, Order(5)]
        public void CreateRevue_WithoutRequiredFields_Returns400()
        {
            var body = new
            {
                title = "",
                url = "",
                description = ""
            };

            var request = new RestRequest("/api/Revue/Create", Method.Post);
            request.AddJsonBody(body);

            var response = client.Execute(request);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        // 1.8 Edit a Non-existing Revue
[Test, Order(6)]
public void Edit_NonExisting_Returns400_And_NoSuchMsg()
{
    var fakeId = System.Guid.NewGuid().ToString();

    var body = new
    {
        title = "X",
        url = "",
        description = "X"
    };

    var request = new RestRequest("/api/Revue/Edit", Method.Put)
        .AddQueryParameter("revueId", fakeId);
    request.AddJsonBody(body);

    var response = client.Execute(request);

    Assert.That(response.StatusCode == HttpStatusCode.BadRequest ||
                response.StatusCode == HttpStatusCode.NotFound,
                $"Expected 400 or 404, got {(int)response.StatusCode}");

    if (!string.IsNullOrWhiteSpace(response.Content))
    {
        using var doc = JsonDocument.Parse(response.Content);
        if (doc.RootElement.ValueKind == JsonValueKind.Object &&
            doc.RootElement.TryGetProperty("msg", out var msgProp) &&
            msgProp.ValueKind == JsonValueKind.String)
        {
            Assert.That(msgProp.GetString(), Is.EqualTo("There is no such revue!"));
        }
    }
}

// 1.9 Delete a Non-existing Revue
[Test, Order(7)]
public void Delete_NonExisting_Returns400_And_NoSuchMsg()
{
    var fakeId = System.Guid.NewGuid().ToString();

    var request = new RestRequest("/api/Revue/Delete", Method.Delete)
        .AddQueryParameter("revueId", fakeId);

    var response = client.Execute(request);

    Assert.That(response.StatusCode == HttpStatusCode.BadRequest ||
                response.StatusCode == HttpStatusCode.NotFound,
                $"Expected 400 or 404, got {(int)response.StatusCode}");

    if (!string.IsNullOrWhiteSpace(response.Content))
    {
        using var doc = JsonDocument.Parse(response.Content);
        if (doc.RootElement.ValueKind == JsonValueKind.Object &&
            doc.RootElement.TryGetProperty("msg", out var msgProp) &&
            msgProp.ValueKind == JsonValueKind.String)
        {
            Assert.That(msgProp.GetString(), Is.EqualTo("There is no such revue!"));
        }
    }
}


        [OneTimeTearDown]
        public void Cleanup()
        {
            client?.Dispose();
        }
    }
}
