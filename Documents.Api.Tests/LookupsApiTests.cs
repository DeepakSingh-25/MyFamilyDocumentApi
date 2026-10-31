using System.Net;
using System.Net.Http.Json;
using Documents.Core.Models;
using Documents.Core.Rules;

namespace Documents.Api.Tests
{
    /// <summary>Spec 013: user ki apni categories / relationships — ownership, duplicates, in-use delete.</summary>
    [Collection(ApiCollection.Name)]
    public sealed class LookupsApiTests(ApiFactory factory)
    {
        [Theory]
        [InlineData("GET", "/api/categories")]
        [InlineData("POST", "/api/categories")]
        [InlineData("PUT", "/api/categories/3f2504e0-4f89-11d3-9a0c-0305e82c3301")]
        [InlineData("DELETE", "/api/categories/3f2504e0-4f89-11d3-9a0c-0305e82c3301")]
        [InlineData("GET", "/api/relationships")]
        [InlineData("POST", "/api/relationships")]
        [InlineData("PUT", "/api/relationships/3f2504e0-4f89-11d3-9a0c-0305e82c3301")]
        [InlineData("DELETE", "/api/relationships/3f2504e0-4f89-11d3-9a0c-0305e82c3301")]
        public async Task Lookups_WithoutToken_Return401(string method, string url)
        {
            var client = factory.CreateClient();

            var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), url)
            {
                Content = method is "POST" or "PUT" ? JsonContent.Create(new { name = "X" }) : null,
            });

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        // ---------- Categories ----------

        [Fact]
        public async Task Categories_List_HasSevenSystemCategoriesWithOtherLast()
        {
            var client = await factory.CreateUserClientAsync();

            var list = await client.GetFromJsonAsync<List<DocumentCategoryDto>>("/api/categories");

            Assert.Equal(7, list!.Count);
            Assert.All(list, c => Assert.True(c.IsSystem));
            Assert.Equal(TestApi.MedicalCategory, list.Single(c => c.Name == "Medical Documents").Id);
            Assert.Equal("Other Documents", list[^1].Name);
        }

        [Fact]
        public async Task CreateCategory_Valid_Returns201AndOnlyOwnerSeesIt()
        {
            var owner = await factory.CreateUserClientAsync();
            var other = await factory.CreateUserClientAsync();

            var response = await owner.PostAsJsonAsync("/api/categories", new { name = "  Pets  ", examples = " Vaccination cards " });

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var created = await response.ReadAsync<DocumentCategoryDto>();
            Assert.Equal("Pets", created.Name);
            Assert.Equal("Vaccination cards", created.Examples);
            Assert.False(created.IsSystem);
            Assert.NotEqual(Guid.Empty, created.Id);
            Assert.DoesNotContain(created.Id, Enumerable.Range(1, 7).Select(DomainRules.SystemCategoryId));

            var ownList = await owner.GetFromJsonAsync<List<DocumentCategoryDto>>("/api/categories");
            Assert.Contains(ownList!, c => c.Id == created.Id);
            // Custom "Other Documents" se pehle
            Assert.Equal("Other Documents", ownList![^1].Name);
            var otherList = await other.GetFromJsonAsync<List<DocumentCategoryDto>>("/api/categories");
            Assert.DoesNotContain(otherList!, c => c.Id == created.Id);
        }

        [Theory]
        [InlineData("pets")]
        [InlineData("medical documents")]
        public async Task CreateCategory_DuplicateOwnOrSystemName_Returns409(string name)
        {
            var client = await factory.CreateUserClientAsync();
            await client.CreateCategoryAsync("Pets");

            var response = await client.PostAsJsonAsync("/api/categories", new { name });

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("A category with this name already exists.", await response.DetailAsync());
        }

        [Fact]
        public async Task CreateCategory_SameNameAsOtherUsersCategory_Returns201()
        {
            var first = await factory.CreateUserClientAsync();
            var second = await factory.CreateUserClientAsync();
            await first.CreateCategoryAsync("Pets");

            var response = await second.PostAsJsonAsync("/api/categories", new { name = "Pets" });

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        public static TheoryData<object, string> InvalidCategoryBodies => new()
        {
            { new { name = "   " }, "Name is required." },
            { new { name = new string('a', 61) }, "Name is too long." },
            { new { name = "Pets", examples = new string('a', 301) }, "Hint is too long." },
        };

        [Theory]
        [MemberData(nameof(InvalidCategoryBodies))]
        public async Task CreateCategory_InvalidBody_Returns400(object body, string expected)
        {
            var client = await factory.CreateUserClientAsync();

            var response = await client.PostAsJsonAsync("/api/categories", body);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(expected, await response.DetailAsync());
        }

        [Theory]
        [InlineData("/api/categories")]
        [InlineData("/api/relationships")]
        public async Task Create_OverPerUserLimit_Returns400(string url)
        {
            var client = await factory.CreateUserClientAsync();
            for (var i = 1; i <= DomainRules.CustomLookupMax; i++)
                (await client.PostAsJsonAsync(url, new { name = $"Item {i}" })).EnsureSuccessStatusCode();

            var response = await client.PostAsJsonAsync(url, new { name = "One too many" });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal($"You can add at most {DomainRules.CustomLookupMax} of your own items.", await response.DetailAsync());
        }

        [Theory]
        [InlineData("/api/categories", "Self​")]
        [InlineData("/api/relationships", "Self​")]
        [InlineData("/api/relationships", "Un\u0007cle")]
        public async Task Create_NameWithHiddenCharacters_Returns400(string url, string name)
        {
            var client = await factory.CreateUserClientAsync();

            var response = await client.PostAsJsonAsync(url, new { name });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("The text contains characters that are not allowed.", await response.DetailAsync());
        }

        [Fact]
        public async Task SystemCategory_UpdateOrDelete_Returns403()
        {
            var client = await factory.CreateUserClientAsync();

            var put = await client.PutAsJsonAsync($"/api/categories/{TestApi.MedicalCategory}", new { name = "Health" });
            var delete = await client.DeleteAsync($"/api/categories/{TestApi.MedicalCategory}");

            Assert.Equal(HttpStatusCode.Forbidden, put.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
            var list = await client.GetFromJsonAsync<List<DocumentCategoryDto>>("/api/categories");
            Assert.Equal("Medical Documents", list!.Single(c => c.Id == TestApi.MedicalCategory).Name);
        }

        [Fact]
        public async Task OtherUsersCategory_UpdateDeleteOrUse_Returns404Or400AndUnchanged()
        {
            var owner = await factory.CreateUserClientAsync();
            var intruder = await factory.CreateUserClientAsync();
            var pets = await owner.CreateCategoryAsync("Pets");
            var member = await intruder.CreateMemberAsync("Ravi");

            var put = await intruder.PutAsJsonAsync($"/api/categories/{pets.Id}", new { name = "Hacked" });
            var delete = await intruder.DeleteAsync($"/api/categories/{pets.Id}");
            var upload = await intruder.PostDocumentFormAsync(TestApi.DocumentForm("Report", member.Id, pets.Id, TestApi.PdfBytes()));

            Assert.Equal(HttpStatusCode.NotFound, put.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, upload.StatusCode);
            Assert.Equal("Please choose a category.", await upload.DetailAsync());
            var ownList = await owner.GetFromJsonAsync<List<DocumentCategoryDto>>("/api/categories");
            Assert.Equal("Pets", ownList!.Single(c => c.Id == pets.Id).Name);
        }

        [Fact]
        public async Task UpdateCategory_Own_RenamesAndDocumentsShowNewName()
        {
            var client = await factory.CreateUserClientAsync();
            var pets = await client.CreateCategoryAsync("Pets");
            var member = await client.CreateMemberAsync("Asha");
            var doc = await client.UploadDocumentAsync(member.Id, pets.Id);

            var response = await client.PutAsJsonAsync($"/api/categories/{pets.Id}", new { name = "Pet Records", examples = "Vet bills" });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var updated = await response.ReadAsync<DocumentCategoryDto>();
            Assert.Equal("Pet Records", updated.Name);
            Assert.Equal("Vet bills", updated.Examples);
            var reloaded = await client.GetFromJsonAsync<FamilyDocumentDto>($"/api/documents/{doc.Id}");
            Assert.Equal("Pet Records", reloaded!.CategoryName);
        }

        [Fact]
        public async Task UpdateCategory_ToSystemName_Returns409()
        {
            var client = await factory.CreateUserClientAsync();
            var pets = await client.CreateCategoryAsync("Pets");

            var response = await client.PutAsJsonAsync($"/api/categories/{pets.Id}", new { name = "IDENTITY DOCUMENTS" });

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        [Fact]
        public async Task DeleteCategory_InUse_Returns409_ThenAfterDocumentDeleted_Returns204()
        {
            var client = await factory.CreateUserClientAsync();
            var pets = await client.CreateCategoryAsync("Pets");
            var member = await client.CreateMemberAsync("Asha");
            var doc = await client.UploadDocumentAsync(member.Id, pets.Id);

            var blocked = await client.DeleteAsync($"/api/categories/{pets.Id}");
            Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
            Assert.Equal("This category has documents. Move them to another category first.", await blocked.DetailAsync());

            (await client.DeleteAsync($"/api/documents/{doc.Id}")).EnsureSuccessStatusCode();
            var deleted = await client.DeleteAsync($"/api/categories/{pets.Id}");

            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
            var list = await client.GetFromJsonAsync<List<DocumentCategoryDto>>("/api/categories");
            Assert.DoesNotContain(list!, c => c.Id == pets.Id);
            // Deactivated row ab use nahi ho sakti, lekin wahi naam dobara ban sakta hai
            Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/categories/{pets.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/categories", new { name = "Pets" })).StatusCode);
        }

        [Fact]
        public async Task Dashboard_ByCategory_HasOwnCustomButNotOtherUsers()
        {
            var owner = await factory.CreateUserClientAsync();
            var other = await factory.CreateUserClientAsync();
            var mine = await owner.CreateCategoryAsync("Pets");
            var theirs = await other.CreateCategoryAsync("Hobbies");
            var member = await owner.CreateMemberAsync("Asha");
            await owner.UploadDocumentAsync(member.Id, mine.Id);

            var summary = await owner.GetFromJsonAsync<DashboardSummaryDto>("/api/dashboard");

            Assert.Equal(1, summary!.ByCategory.Single(c => c.Category.Id == mine.Id).Count);
            Assert.DoesNotContain(summary.ByCategory, c => c.Category.Id == theirs.Id);
        }

        // ---------- Relationships ----------

        [Fact]
        public async Task Relationships_List_HasThirteenSystemWithSelfFirstAndOtherLast()
        {
            var client = await factory.CreateUserClientAsync();

            var list = await client.GetFromJsonAsync<List<RelationshipDto>>("/api/relationships");

            Assert.Equal(13, list!.Count);
            Assert.All(list, r => Assert.True(r.IsSystem));
            Assert.Equal(new RelationshipDto(Guid.Parse("20000000-0000-0000-0000-000000000001"), "Self", true, 10), list[0]);
            Assert.Equal("Other", list[^1].Name);
        }

        [Fact]
        public async Task CreateRelationship_UsedByMember_ShowsNameAndOnlyOwnerCanUseIt()
        {
            var owner = await factory.CreateUserClientAsync();
            var other = await factory.CreateUserClientAsync();
            var uncle = await owner.CreateRelationshipAsync("Uncle");

            var created = await owner.PostMemberAsync("Mohan", uncle.Id);
            var intruder = await other.PostMemberAsync("Mohan", uncle.Id);

            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var member = await created.ReadAsync<FamilyMemberDto>();
            Assert.Equal(uncle.Id, member.RelationshipId);
            Assert.Equal("Uncle", member.Relationship);
            Assert.Equal(HttpStatusCode.BadRequest, intruder.StatusCode);
            Assert.Equal("Please choose a relationship.", await intruder.DetailAsync());
            var otherList = await other.GetFromJsonAsync<List<RelationshipDto>>("/api/relationships");
            Assert.DoesNotContain(otherList!, r => r.Id == uncle.Id);
        }

        [Theory]
        [InlineData("uncle")]
        [InlineData("FATHER")]
        public async Task CreateRelationship_DuplicateOwnOrSystemName_Returns409(string name)
        {
            var client = await factory.CreateUserClientAsync();
            await client.CreateRelationshipAsync("Uncle");

            var response = await client.PostAsJsonAsync("/api/relationships", new { name });

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("A relationship with this name already exists.", await response.DetailAsync());
        }

        [Fact]
        public async Task CreateRelationship_TooLong_Returns400()
        {
            var client = await factory.CreateUserClientAsync();

            var response = await client.PostAsJsonAsync("/api/relationships", new { name = new string('a', 31) });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("Name is too long.", await response.DetailAsync());
        }

        [Fact]
        public async Task SystemRelationship_UpdateOrDelete_Returns403()
        {
            var client = await factory.CreateUserClientAsync();

            var put = await client.PutAsJsonAsync($"/api/relationships/{DomainRules.SelfRelationshipId}", new { name = "Me" });
            var delete = await client.DeleteAsync($"/api/relationships/{DomainRules.SelfRelationshipId}");

            Assert.Equal(HttpStatusCode.Forbidden, put.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
        }

        [Fact]
        public async Task OtherUsersRelationship_UpdateOrDelete_Returns404AndUnchanged()
        {
            var owner = await factory.CreateUserClientAsync();
            var intruder = await factory.CreateUserClientAsync();
            var uncle = await owner.CreateRelationshipAsync("Uncle");

            var put = await intruder.PutAsJsonAsync($"/api/relationships/{uncle.Id}", new { name = "Hacked" });
            var delete = await intruder.DeleteAsync($"/api/relationships/{uncle.Id}");

            Assert.Equal(HttpStatusCode.NotFound, put.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
            var list = await owner.GetFromJsonAsync<List<RelationshipDto>>("/api/relationships");
            Assert.Equal("Uncle", list!.Single(r => r.Id == uncle.Id).Name);
        }

        [Fact]
        public async Task UpdateRelationship_Own_MemberShowsNewName()
        {
            var client = await factory.CreateUserClientAsync();
            var uncle = await client.CreateRelationshipAsync("Uncle");
            var member = await (await client.PostMemberAsync("Mohan", uncle.Id)).ReadAsync<FamilyMemberDto>();

            var response = await client.PutAsJsonAsync($"/api/relationships/{uncle.Id}", new { name = "Chacha" });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var reloaded = await client.GetFromJsonAsync<FamilyMemberDto>($"/api/members/{member.Id}");
            Assert.Equal("Chacha", reloaded!.Relationship);
        }

        [Fact]
        public async Task DeleteRelationship_InUse_Returns409_ThenAfterMemberDeleted_Returns204()
        {
            var client = await factory.CreateUserClientAsync();
            var uncle = await client.CreateRelationshipAsync("Uncle");
            var member = await (await client.PostMemberAsync("Mohan", uncle.Id)).ReadAsync<FamilyMemberDto>();

            var blocked = await client.DeleteAsync($"/api/relationships/{uncle.Id}");
            Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);

            // Soft-deleted member ki FK rehti hai — phir bhi relationship hat sakta hai (deactivate)
            (await client.DeleteAsync($"/api/members/{member.Id}")).EnsureSuccessStatusCode();
            var deleted = await client.DeleteAsync($"/api/relationships/{uncle.Id}");

            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
            var list = await client.GetFromJsonAsync<List<RelationshipDto>>("/api/relationships");
            Assert.DoesNotContain(list!, r => r.Id == uncle.Id);
            var reuse = await client.PostMemberAsync("Ravi", uncle.Id);
            Assert.Equal(HttpStatusCode.BadRequest, reuse.StatusCode);
        }
    }

    internal static class LookupTestApi
    {
        public static async Task<DocumentCategoryDto> CreateCategoryAsync(this HttpClient client, string name)
        {
            var response = await client.PostAsJsonAsync("/api/categories", new { name });
            response.EnsureSuccessStatusCode();
            return await response.ReadAsync<DocumentCategoryDto>();
        }

        public static async Task<RelationshipDto> CreateRelationshipAsync(this HttpClient client, string name)
        {
            var response = await client.PostAsJsonAsync("/api/relationships", new { name });
            response.EnsureSuccessStatusCode();
            return await response.ReadAsync<RelationshipDto>();
        }
    }
}
