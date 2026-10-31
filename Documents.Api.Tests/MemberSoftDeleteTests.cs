using System.Net;
using System.Net.Http.Json;
using Documents.Core.Models;

namespace Documents.Api.Tests
{
    /// <summary>Spec 007 §B.5 / §B.9 criteria 8, 13, 14: soft delete aur SQL restore.</summary>
    [Collection(ApiCollection.Name)]
    public sealed class MemberSoftDeleteTests(ApiFactory factory)
    {
        private sealed record Deleted(HttpClient Client, FamilyMemberDto Member, FamilyDocumentDto Medical, FamilyDocumentDto Identity, int FilesBefore);

        /// <summary>Asha + 2 documents (1 Medical), phir DELETE.</summary>
        private async Task<Deleted> CreateAndDeleteAshaAsync()
        {
            var client = await factory.CreateUserClientAsync();
            var asha = await client.CreateMemberAsync("Asha", "Mother");
            var medical = await client.UploadDocumentAsync(asha.Id, TestApi.MedicalCategory, "Blood test");
            var identity = await client.UploadDocumentAsync(asha.Id, TestApi.IdentityCategory, "Aadhaar");
            var files = factory.CountStoredFiles();

            var response = await client.DeleteAsync($"/api/members/{asha.Id}");
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            return new Deleted(client, asha, medical, identity, files);
        }

        [Fact]
        public async Task Delete_KeepsMemberRowDocumentsAndFiles()
        {
            var d = await CreateAndDeleteAshaAsync();

            var row = await factory.GetMemberRowAsync(d.Member.Id);
            Assert.True(row.Exists);
            Assert.NotNull(row.DeletedOn);
            Assert.InRange(row.DeletedOn!.Value, DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddMinutes(1));
            Assert.Equal(2, await factory.CountDocumentRowsAsync(d.Member.Id));
            Assert.Equal(d.FilesBefore, factory.CountStoredFiles());
        }

        [Fact]
        public async Task Delete_HidesMemberEverywhere()
        {
            var d = await CreateAndDeleteAshaAsync();

            var get = await d.Client.GetAsync($"/api/members/{d.Member.Id}");
            var list = await d.Client.GetFromJsonAsync<List<FamilyMemberDto>>("/api/members");
            var again = await d.Client.DeleteAsync($"/api/members/{d.Member.Id}");

            Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
            Assert.DoesNotContain(list!, m => m.Id == d.Member.Id);
            Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
        }

        [Theory]
        [InlineData("/api/documents")]
        [InlineData("/api/documents?search=Blood")]
        [InlineData("/api/documents?categoryId={medical}")]
        [InlineData("/api/documents?memberId={member}")]
        public async Task Delete_HidesDocumentsFromLists(string url)
        {
            var d = await CreateAndDeleteAshaAsync();

            var page = await d.Client.GetDocumentPageAsync(url.Replace("/api/documents", "").Replace("{member}", d.Member.Id.ToString())
                .Replace("{medical}", TestApi.MedicalCategory.ToString()));

            Assert.Empty(page.Items);
            Assert.Equal(0, page.TotalCount);
        }

        [Fact]
        public async Task Delete_DocumentEndpoints_Return404()
        {
            var d = await CreateAndDeleteAshaAsync();
            var id = d.Medical.Id;

            var get = await d.Client.GetAsync($"/api/documents/{id}");
            var file = await d.Client.GetAsync($"/api/documents/{id}/file");
            using var form = new MultipartFormDataContent
            {
                { new StringContent("Renamed"), "name" },
                { new StringContent(d.Member.Id.ToString()), "memberId" },
                { new StringContent(TestApi.MedicalCategory.ToString()), "categoryId" },
            };
            var put = await d.Client.PutAsync($"/api/documents/{id}", form);
            var delete = await d.Client.DeleteAsync($"/api/documents/{id}");

            Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, file.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, put.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
            Assert.Equal(2, await factory.CountDocumentRowsAsync(d.Member.Id));
        }

        [Fact]
        public async Task Delete_UploadForDeletedMember_Returns400()
        {
            var d = await CreateAndDeleteAshaAsync();

            var response = await d.Client.PostDocumentAsync(d.Member.Id);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("Please choose a family member.", await response.DetailAsync());
        }

        [Fact]
        public async Task Delete_DashboardLeavesThemOut()
        {
            var d = await CreateAndDeleteAshaAsync();

            var summary = await d.Client.GetFromJsonAsync<DashboardSummaryDto>("/api/dashboard");

            Assert.Equal(0, summary!.TotalMembers);
            Assert.Equal(0, summary.TotalDocuments);
            Assert.Equal(0, summary.MedicalDocuments);
            Assert.Empty(summary.Recent);
            Assert.All(summary.ByCategory, c => Assert.Equal(0, c.Count));
        }

        [Fact]
        public async Task Delete_SameNameCanBeAddedAgain()
        {
            var d = await CreateAndDeleteAshaAsync();

            var response = await d.Client.PostMemberAsync("Asha", "Mother");

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        [Fact]
        public async Task RestoreSql_BringsBackMemberAndDocuments()
        {
            var d = await CreateAndDeleteAshaAsync();

            var rows = await factory.ExecuteSqlAsync(
                $"UPDATE FamilyMembers SET DeletedOn = NULL WHERE Id = {d.Member.Id} AND DeletedOn IS NOT NULL");

            Assert.Equal(1, rows);
            var member = await d.Client.GetFromJsonAsync<FamilyMemberDto>($"/api/members/{d.Member.Id}");
            Assert.Equal(2, member!.DocumentCount);
            var docs = (await d.Client.GetDocumentPageAsync($"?memberId={d.Member.Id}")).Items;
            Assert.Equal([d.Identity.Id, d.Medical.Id], docs!.Select(x => x.Id));
            Assert.Equal(d.Medical.UploadedOn, docs!.Single(x => x.Id == d.Medical.Id).UploadedOn);
            foreach (var docId in new[] { d.Medical.Id, d.Identity.Id })
            {
                var file = await d.Client.GetAsync($"/api/documents/{docId}/file");
                Assert.Equal(HttpStatusCode.OK, file.StatusCode);
            }
        }

        [Fact]
        public async Task RestoreSql_ActiveNameClash_FailsUntilRenamed()
        {
            var d = await CreateAndDeleteAshaAsync();
            await d.Client.CreateMemberAsync("Asha", "Mother");

            await Assert.ThrowsAnyAsync<Exception>(() => factory.ExecuteSqlAsync(
                $"UPDATE FamilyMembers SET DeletedOn = NULL WHERE Id = {d.Member.Id} AND DeletedOn IS NOT NULL"));
            await factory.ExecuteSqlAsync(
                $"UPDATE FamilyMembers SET Name = LEFT(Name, 89) + N' (restored)' WHERE Id = {d.Member.Id} AND DeletedOn IS NOT NULL");
            var rows = await factory.ExecuteSqlAsync(
                $"UPDATE FamilyMembers SET DeletedOn = NULL WHERE Id = {d.Member.Id} AND DeletedOn IS NOT NULL");

            Assert.Equal(1, rows);
            var list = await d.Client.GetFromJsonAsync<List<FamilyMemberDto>>("/api/members");
            Assert.Contains(list!, m => m.Name == "Asha (restored)");
        }
    }
}
