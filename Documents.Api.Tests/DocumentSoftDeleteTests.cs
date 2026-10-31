using System.Net;
using System.Net.Http.Json;
using Documents.Core.Models;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Documents.Api.Tests
{
    /// <summary>Spec 009 §B.2 / §B.9 criteria 13–15: document soft delete aur SQL restore.</summary>
    [Collection(ApiCollection.Name)]
    public sealed class DocumentSoftDeleteTests(ApiFactory factory)
    {
        private sealed record Deleted(HttpClient Client, FamilyMemberDto Asha, FamilyDocumentDto Gone, FamilyDocumentDto Kept, string GoneKey);

        /// <summary>Asha + 2 documents (dono Medical), phir ek ka DELETE.</summary>
        private async Task<Deleted> CreateAndDeleteOneAsync()
        {
            var client = await factory.CreateUserClientAsync();
            var asha = await client.CreateMemberAsync("Asha", "Mother");
            var gone = await client.UploadDocumentAsync(asha.Id, TestApi.MedicalCategory, "Blood test");
            var kept = await client.UploadDocumentAsync(asha.Id, TestApi.MedicalCategory, "X-ray");
            var key = (await factory.GetDocumentRowAsync(gone.Id)).StorageKey!;

            var response = await client.DeleteAsync($"/api/documents/{gone.Id}");
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            return new Deleted(client, asha, gone, kept, key);
        }

        private Task<int> RestoreDocumentAsync(Guid id) =>
            factory.ExecuteSqlAsync($"UPDATE Documents SET DeletedOn = NULL WHERE Id = {id} AND DeletedOn IS NOT NULL");

        private Task<int> RestoreMemberAsync(Guid id) =>
            factory.ExecuteSqlAsync($"UPDATE FamilyMembers SET DeletedOn = NULL WHERE Id = {id} AND DeletedOn IS NOT NULL");

        // ---------- Criterion 13 ----------

        [Fact]
        public async Task Delete_KeepsRowAndFile()
        {
            var d = await CreateAndDeleteOneAsync();

            var row = await factory.GetDocumentRowAsync(d.Gone.Id);

            Assert.True(row.Exists);
            Assert.NotNull(row.DeletedOn);
            Assert.InRange(row.DeletedOn!.Value, DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddMinutes(1));
            Assert.True(factory.StoredFileExists(d.GoneKey));
        }

        [Fact]
        public async Task Delete_DocumentEndpoints_Return404()
        {
            var d = await CreateAndDeleteOneAsync();
            var id = d.Gone.Id;

            var get = await d.Client.GetAsync($"/api/documents/{id}");
            var file = await d.Client.GetAsync($"/api/documents/{id}/file");
            var put = await d.Client.PutDocumentFormAsync(id, TestApi.DocumentForm("Renamed", d.Asha.Id, TestApi.MedicalCategory));
            var again = await d.Client.DeleteAsync($"/api/documents/{id}");

            foreach (var response in new[] { get, file, put, again })
            {
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                Assert.Equal("Document not found.", await response.DetailAsync());
            }
            Assert.True(factory.StoredFileExists(d.GoneKey));
        }

        [Fact]
        public async Task Delete_ListCountsDashboardAndMemberLeaveItOut()
        {
            var d = await CreateAndDeleteOneAsync();

            var page = await d.Client.GetDocumentPageAsync();
            var counts = await d.Client.GetFromJsonAsync<DocumentCountsDto>($"/api/documents/counts?categoryId={TestApi.MedicalCategory}");
            var summary = await d.Client.GetFromJsonAsync<DashboardSummaryDto>("/api/dashboard");
            var member = await d.Client.GetFromJsonAsync<FamilyMemberDto>($"/api/members/{d.Asha.Id}");

            Assert.Equal(1, page.TotalCount);
            Assert.Equal([d.Kept.Id], page.Items.Select(x => x.Id));
            Assert.Equal(1, counts!.Total);
            Assert.Equal(1, summary!.TotalDocuments);
            Assert.Equal(1, summary.MedicalDocuments);
            Assert.Equal(1, summary.AddedThisMonth);
            Assert.DoesNotContain(summary.Recent, x => x.Id == d.Gone.Id);
            Assert.Equal(1, summary.ByCategory.Single(c => c.Category.Id == TestApi.MedicalCategory).Count);
            Assert.Equal(1, member!.DocumentCount);
        }

        [Fact]
        public async Task Delete_DoesNotChangeUpdatedOn()
        {
            var d = await CreateAndDeleteOneAsync();
            await RestoreDocumentAsync(d.Gone.Id);

            var doc = await d.Client.GetFromJsonAsync<FamilyDocumentDto>($"/api/documents/{d.Gone.Id}");

            Assert.Null(doc!.UpdatedOn);
            Assert.Equal(d.Gone.UploadedOn, doc.UploadedOn);
        }

        [Fact]
        public async Task DeleteMember_AfterDocumentDelete_HidesOtherDocumentToo()
        {
            var d = await CreateAndDeleteOneAsync();

            Assert.Equal(HttpStatusCode.NoContent, (await d.Client.DeleteAsync($"/api/members/{d.Asha.Id}")).StatusCode);

            var page = await d.Client.GetDocumentPageAsync();
            var kept = await d.Client.GetAsync($"/api/documents/{d.Kept.Id}");
            Assert.Equal(0, page.TotalCount);
            Assert.Equal(HttpStatusCode.NotFound, kept.StatusCode);
            // Member delete document ka DeletedOn nahi chhoota
            Assert.Null((await factory.GetDocumentRowAsync(d.Kept.Id)).DeletedOn);
        }

        // ---------- Criterion 14: restore runbook ----------

        [Fact]
        public async Task RestoreSql_Document_ComesBackWithSameDataAndFile()
        {
            var d = await CreateAndDeleteOneAsync();

            var rows = await RestoreDocumentAsync(d.Gone.Id);

            Assert.Equal(1, rows);
            var doc = await d.Client.GetFromJsonAsync<FamilyDocumentDto>($"/api/documents/{d.Gone.Id}");
            Assert.Equal(d.Gone, doc);
            var file = await d.Client.GetAsync($"/api/documents/{d.Gone.Id}/file");
            Assert.Equal(HttpStatusCode.OK, file.StatusCode);
        }

        [Fact]
        public async Task RestoreSql_DocumentWhileMemberDeleted_StaysHiddenUntilMemberRestored()
        {
            var d = await CreateAndDeleteOneAsync();
            await d.Client.DeleteAsync($"/api/members/{d.Asha.Id}");

            Assert.Equal(1, await RestoreDocumentAsync(d.Gone.Id));
            var hidden = await d.Client.GetAsync($"/api/documents/{d.Gone.Id}");
            Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
            Assert.Equal(0, (await d.Client.GetDocumentPageAsync()).TotalCount);

            Assert.Equal(1, await RestoreMemberAsync(d.Asha.Id));
            var page = await d.Client.GetDocumentPageAsync($"?memberId={d.Asha.Id}");
            Assert.Equal([d.Kept, d.Gone], page.Items);
            foreach (var id in new[] { d.Gone.Id, d.Kept.Id })
                Assert.Equal(HttpStatusCode.OK, (await d.Client.GetAsync($"/api/documents/{id}/file")).StatusCode);
        }

        [Fact]
        public async Task RestoreSql_MemberOnly_BringsBackOnlyNotDeletedDocuments()
        {
            var d = await CreateAndDeleteOneAsync();
            await d.Client.DeleteAsync($"/api/members/{d.Asha.Id}");

            Assert.Equal(1, await RestoreMemberAsync(d.Asha.Id));

            var page = await d.Client.GetDocumentPageAsync();
            var member = await d.Client.GetFromJsonAsync<FamilyMemberDto>($"/api/members/{d.Asha.Id}");
            Assert.Equal([d.Kept.Id], page.Items.Select(x => x.Id));
            Assert.Equal(1, member!.DocumentCount);
        }

        // ---------- Criterion 15 ----------

        [Fact]
        public void Source_NeverIgnoresQueryFilters()
        {
            var backend = FindBackendFolder();
            var forbidden = "Ignore" + "QueryFilters";

            var hits = Directory.EnumerateFiles(backend, "*.cs", SearchOption.AllDirectories)
                .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                         && !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
                .Where(p => File.ReadAllText(p).Contains(forbidden))
                .ToList();

            Assert.Empty(hits);
        }

        [Fact]
        public void Endpoints_NoRestoreRoute()
        {
            var routes = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
                .OfType<RouteEndpoint>()
                .Select(e => e.RoutePattern.RawText ?? "");

            Assert.DoesNotContain(routes, r => r.Contains("restore", StringComparison.OrdinalIgnoreCase));
        }

        private static string FindBackendFolder()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Documents.Api.sln"))) dir = dir.Parent;
            return dir?.FullName ?? throw new InvalidOperationException("Documents.Api.sln not found above the test output.");
        }
    }
}
