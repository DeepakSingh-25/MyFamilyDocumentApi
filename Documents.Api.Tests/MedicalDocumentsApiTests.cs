using System.Net;
using System.Net.Http.Json;
using Documents.Core.Models;

namespace Documents.Api.Tests
{
    /// <summary>Spec 011 §B.4 items 1–7: Medical page ke list, counts, search, move, soft delete aur ownership.</summary>
    [Collection(ApiCollection.Name)]
    public sealed class MedicalDocumentsApiTests(ApiFactory factory)
    {
        private const string Apollo = "Apollo Hospital";
        private static readonly string MedicalQuery = $"?categoryId={TestApi.MedicalCategory}";
        private static readonly string CountsUrl = $"/api/documents/counts?categoryId={TestApi.MedicalCategory}";

        private sealed record MedicalFamily(HttpClient Client, Guid Asha, Guid Ravi, List<Guid> AshaMedical, List<Guid> RaviMedical, List<Guid> ApolloIds);

        /// <summary>
        /// Asha: 12 medical (2 ka hospital "Apollo Hospital") + 3 identity. Ravi: 4 medical (1 "Apollo Hospital").
        /// Kisi bhi document ke naam mein "apollo" nahi hai — search sirf hospitalOrDoctor se match ho.
        /// </summary>
        private async Task<MedicalFamily> SeedFamilyAsync()
        {
            var client = await factory.CreateUserClientAsync();
            var asha = await client.CreateMemberAsync("Asha", "Mother");
            var ravi = await client.CreateMemberAsync("Ravi", "Father");
            var ashaMedical = new List<Guid>();
            var raviMedical = new List<Guid>();
            var apollo = new List<Guid>();

            for (var i = 0; i < 12; i++)
            {
                var hospital = i is 3 or 8 ? Apollo : "City Clinic";
                var id = await UploadAsync(client, $"Asha report {i:D2}", asha.Id, TestApi.MedicalCategory, hospital);
                ashaMedical.Add(id);
                if (hospital == Apollo) apollo.Add(id);
            }
            for (var i = 0; i < 3; i++)
                await UploadAsync(client, $"Asha ID {i}", asha.Id, TestApi.IdentityCategory);
            for (var i = 0; i < 4; i++)
            {
                var hospital = i == 1 ? Apollo : null;
                var id = await UploadAsync(client, $"Ravi report {i}", ravi.Id, TestApi.MedicalCategory, hospital);
                raviMedical.Add(id);
                if (hospital == Apollo) apollo.Add(id);
            }

            return new MedicalFamily(client, asha.Id, ravi.Id, ashaMedical, raviMedical, apollo);
        }

        private static async Task<Guid> UploadAsync(HttpClient client, string name, Guid memberId, Guid categoryId, string? hospital = null)
        {
            var response = await client.PostDocumentFormAsync(
                TestApi.DocumentForm(name, memberId, categoryId, TestApi.PdfBytes(), hospitalOrDoctor: hospital));
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            return (await response.ReadAsync<FamilyDocumentDto>()).Id;
        }

        private static async Task<DocumentCountsDto> GetMedicalCountsAsync(HttpClient client) =>
            (await client.GetFromJsonAsync<DocumentCountsDto>(CountsUrl))!;

        private static async Task<DashboardSummaryDto> GetDashboardAsync(HttpClient client) =>
            (await client.GetFromJsonAsync<DashboardSummaryDto>("/api/dashboard"))!;

        // ---------- Item 1: list by category + member ----------

        [Fact]
        public async Task List_MedicalCategory_CountsBothMembersButNotIdentity()
        {
            var f = await SeedFamilyAsync();

            var page = await f.Client.GetDocumentPageAsync(MedicalQuery);

            Assert.Equal(16, page.TotalCount);
            Assert.All(page.Items, d => Assert.Equal(TestApi.MedicalCategory, d.CategoryId));
        }

        [Fact]
        public async Task List_MedicalForAsha_PagesHoldHer12RecordsOnce()
        {
            var f = await SeedFamilyAsync();
            var query = $"{MedicalQuery}&memberId={f.Asha}";

            var first = await f.Client.GetDocumentPageAsync(query);
            var second = await f.Client.GetDocumentPageAsync($"{query}&page=2");

            Assert.Equal((12, 2), (first.TotalCount, first.TotalPages));
            Assert.Equal(10, first.Items.Count);
            Assert.Equal(2, second.Items.Count);
            var all = first.Items.Concat(second.Items).ToList();
            Assert.All(all, d =>
            {
                Assert.Equal(f.Asha, d.MemberId);
                Assert.Equal(TestApi.MedicalCategory, d.CategoryId);
            });
            Assert.Equal(f.AshaMedical.Order(), all.Select(d => d.Id).Distinct().Order());
        }

        // ---------- Item 2: counts ----------

        [Fact]
        public async Task Counts_Medical_GroupsByMemberAndSkipsIdentity()
        {
            var f = await SeedFamilyAsync();

            var counts = await GetMedicalCountsAsync(f.Client);

            Assert.Equal(16, counts.Total);
            Assert.Equal(new[] { new MemberCountDto(f.Asha, 12), new MemberCountDto(f.Ravi, 4) }.OrderBy(x => x.MemberId), counts.ByMember.OrderBy(x => x.MemberId));
            Assert.Equal([new CategoryIdCountDto(TestApi.MedicalCategory, 16)], counts.ByCategory);
        }

        // ---------- Item 3: search on hospital ----------

        [Fact]
        public async Task List_SearchApollo_MatchesByHospitalOnly()
        {
            var f = await SeedFamilyAsync();

            var page = await f.Client.GetDocumentPageAsync($"{MedicalQuery}&search=apollo");

            Assert.Equal(f.ApolloIds.Order(), page.Items.Select(d => d.Id).Order());
            Assert.All(page.Items, d =>
            {
                Assert.DoesNotContain("apollo", d.Name, StringComparison.OrdinalIgnoreCase);
                Assert.Equal(Apollo, d.HospitalOrDoctor);
            });
        }

        // ---------- Item 4: move out of Medical ----------

        [Fact]
        public async Task Update_MedicalToIdentity_DropsHospitalAndLeavesMedicalList()
        {
            var f = await SeedFamilyAsync();
            var moved = f.ApolloIds[0];

            // Form abhi bhi hospital bhej raha hai — non-medical category par wo save nahi hona chahiye
            var response = await f.Client.PutDocumentFormAsync(moved,
                TestApi.DocumentForm("Asha report moved", f.Asha, TestApi.IdentityCategory, hospitalOrDoctor: Apollo));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var updated = await response.ReadAsync<FamilyDocumentDto>();
            Assert.Equal(TestApi.IdentityCategory, updated.CategoryId);
            Assert.Null(updated.HospitalOrDoctor);

            var page = await f.Client.GetDocumentPageAsync($"{MedicalQuery}&pageSize=50");
            var counts = await GetMedicalCountsAsync(f.Client);
            Assert.Equal(15, page.TotalCount);
            Assert.DoesNotContain(page.Items, d => d.Id == moved);
            Assert.Equal(15, counts.Total);
        }

        // ---------- Item 5: soft delete of a medical record ----------

        [Fact]
        public async Task Delete_AshaMedicalRecord_LeavesListCountsAndDashboard()
        {
            var f = await SeedFamilyAsync();

            Assert.Equal(HttpStatusCode.NoContent, (await f.Client.DeleteAsync($"/api/documents/{f.AshaMedical[0]}")).StatusCode);

            var page = await f.Client.GetDocumentPageAsync($"{MedicalQuery}&memberId={f.Asha}");
            var counts = await GetMedicalCountsAsync(f.Client);
            var summary = await GetDashboardAsync(f.Client);
            Assert.Equal(11, page.TotalCount);
            Assert.Equal(11, counts.ByMember.Single(x => x.MemberId == f.Asha).Count);
            Assert.Equal(15, summary.MedicalDocuments);
        }

        [Fact]
        public async Task Delete_OnlyRowOfPage2_Page2ReturnsEmptyWithOnePage()
        {
            var f = await SeedFamilyAsync();
            var query = $"{MedicalQuery}&memberId={f.Asha}";
            Assert.Equal(HttpStatusCode.NoContent, (await f.Client.DeleteAsync($"/api/documents/{f.AshaMedical[0]}")).StatusCode);
            var before = await f.Client.GetDocumentPageAsync($"{query}&page=2");
            var last = Assert.Single(before.Items);

            Assert.Equal(HttpStatusCode.NoContent, (await f.Client.DeleteAsync($"/api/documents/{last.Id}")).StatusCode);

            var after = await f.Client.GetDocumentPageAsync($"{query}&page=2");
            Assert.Empty(after.Items);
            Assert.Equal((2, 10, 1), (after.Page, after.TotalCount, after.TotalPages));
        }

        // ---------- Item 6: soft delete of a member ----------

        [Fact]
        public async Task DeleteMember_Ravi_HisRecordsLeaveListCountsAndDashboard()
        {
            var f = await SeedFamilyAsync();

            Assert.Equal(HttpStatusCode.NoContent, (await f.Client.DeleteAsync($"/api/members/{f.Ravi}")).StatusCode);

            var page = await f.Client.GetDocumentPageAsync($"{MedicalQuery}&pageSize=50");
            var counts = await GetMedicalCountsAsync(f.Client);
            var summary = await GetDashboardAsync(f.Client);
            Assert.Equal(12, page.TotalCount);
            Assert.DoesNotContain(page.Items, d => d.MemberId == f.Ravi);
            Assert.Equal(12, counts.Total);
            Assert.Equal([new MemberCountDto(f.Asha, 12)], counts.ByMember);
            Assert.Equal(12, summary.MedicalDocuments);
        }

        // ---------- Item 7: other user ----------

        [Fact]
        public async Task OtherUser_MedicalListAndCounts_SeeNothingOfAsha()
        {
            var f = await SeedFamilyAsync();
            var other = await factory.CreateUserClientAsync();

            var page = await other.GetDocumentPageAsync($"{MedicalQuery}&memberId={f.Asha}");
            var counts = await GetMedicalCountsAsync(other);

            Assert.Equal(0, page.TotalCount);
            Assert.Empty(page.Items);
            Assert.Equal(0, counts.Total);
            Assert.Empty(counts.ByMember);
        }
    }
}
