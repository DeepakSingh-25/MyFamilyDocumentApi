using System.Net;
using System.Net.Http.Json;
using Documents.Core.Models;
using Documents.Core.Rules;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Documents.Api.Tests
{
    /// <summary>Spec 009 §B.9 criteria 1–12 aur 16: documents CRUD, validation, ownership, paging, counts.</summary>
    [Collection(ApiCollection.Name)]
    public sealed class DocumentsApiTests(ApiFactory factory)
    {
        // ---------- Auth (criteria 1, 2) ----------

        [Theory]
        [InlineData("GET", "/api/documents")]
        [InlineData("GET", "/api/documents/counts")]
        [InlineData("GET", "/api/documents/3f2504e0-4f89-11d3-9a0c-0305e82c3301")]
        [InlineData("GET", "/api/documents/3f2504e0-4f89-11d3-9a0c-0305e82c3301/file")]
        [InlineData("POST", "/api/documents")]
        [InlineData("PUT", "/api/documents/3f2504e0-4f89-11d3-9a0c-0305e82c3301")]
        [InlineData("DELETE", "/api/documents/3f2504e0-4f89-11d3-9a0c-0305e82c3301")]
        public async Task Documents_WithoutToken_Returns401(string method, string url)
        {
            var client = factory.CreateClient();

            var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), url)
            {
                Content = method is "POST" or "PUT" ? TestApi.DocumentForm("A", Guid.NewGuid(), TestApi.IdentityCategory, TestApi.PdfBytes()) : null,
            });

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public void Endpoints_AllDocumentRoutes_RequireAuthorization()
        {
            var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
                .OfType<RouteEndpoint>()
                .Where(e => e.RoutePattern.RawText?.StartsWith("api/documents", StringComparison.OrdinalIgnoreCase) == true)
                .ToList();

            Assert.Equal(7, endpoints.Count);
            Assert.All(endpoints, e =>
            {
                Assert.NotEmpty(e.Metadata.GetOrderedMetadata<IAuthorizeData>());
                Assert.Null(e.Metadata.GetMetadata<IAllowAnonymous>());
            });
        }

        // ---------- GUID ids (spec 014) ----------

        [Theory]
        [InlineData("/api/documents/1")]
        [InlineData("/api/documents/1/file")]
        [InlineData("/api/documents/not-a-guid")]
        [InlineData("/api/members/1")]
        [InlineData("/api/categories/1")]
        public async Task NonGuidRouteId_Returns404(string url)
        {
            var client = await factory.CreateUserClientAsync();

            var response = await client.SendAsync(new HttpRequestMessage(url.StartsWith("/api/categories") ? HttpMethod.Delete : HttpMethod.Get, url));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Theory]
        [InlineData("/api/documents?memberId=1")]
        [InlineData("/api/documents?categoryId=abc")]
        [InlineData("/api/documents/counts?memberId=1")]
        public async Task NonGuidQueryId_Returns400(string url)
        {
            var client = await factory.CreateUserClientAsync();

            var response = await client.GetAsync(url);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task UnknownGuid_Returns404()
        {
            var client = await factory.CreateUserClientAsync();

            var response = await client.GetAsync($"/api/documents/{Guid.NewGuid()}");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("Document not found.", await response.DetailAsync());
        }

        [Fact]
        public async Task Create_ReturnsGuidIds()
        {
            var client = await factory.CreateUserClientAsync();
            var member = await client.CreateMemberAsync("Asha");

            var doc = await client.UploadDocumentAsync(member.Id, TestApi.MedicalCategory);

            Assert.NotEqual(Guid.Empty, doc.Id);
            Assert.Equal(member.Id, doc.MemberId);
            Assert.Equal(Guid.Parse("10000000-0000-0000-0000-000000000003"), doc.CategoryId);
        }

        /// <summary>Spec 014 se pehle ke token mein "sub" int tha — ab 401 (dobara login), kabhi 500 nahi.</summary>
        [Fact]
        public async Task OldTokenWithIntSub_Returns401()
        {
            var jwt = factory.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<Documents.Core.Options.JwtOptions>>().Value;
            var key = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(jwt.SecretKey));
            var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
                issuer: jwt.Issuer,
                audience: jwt.Audience,
                claims:
                [
                    new("sub", "1"),
                    new("pwv", "0"),
                    new("jti", Guid.NewGuid().ToString("N")),
                ],
                notBefore: DateTime.UtcNow,
                expires: DateTime.UtcNow.AddMinutes(5),
                signingCredentials: new(key, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256));
            var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new("Bearer", new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token));

            var response = await client.GetAsync("/api/documents");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        // ---------- Create / validation (criteria 3, 4) ----------

        [Fact]
        public async Task Create_ValidPdf_Returns201WithTrimmedNameAndNoHospital()
        {
            var client = await factory.CreateUserClientAsync();
            var member = await client.CreateMemberAsync("Asha");

            var response = await client.PostDocumentFormAsync(
                TestApi.DocumentForm("  Aadhaar  ", member.Id, TestApi.IdentityCategory, TestApi.PdfBytes(), hospitalOrDoctor: "Apollo"));

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.NotNull(response.Headers.Location);
            var doc = await response.ReadAsync<FamilyDocumentDto>();
            Assert.Equal("Aadhaar", doc.Name);
            Assert.Equal("pdf", doc.FileType);
            Assert.Null(doc.HospitalOrDoctor);
        }

        /// <summary>size: -1 = file nahi bheji; 0 = khaali file; warna PDF bytes itne bade.</summary>
        public static TheoryData<string, long, string> InvalidFiles => new()
        {
            { "scan.png", 64, "The file content does not match its type. Please upload a real PDF, JPG or PNG." },
            { "empty.pdf", 0, "The file is empty." },
            { "big.pdf", FileRules.MaxBytes + 1, "File is too large. Maximum size is 10 MB." },
            { "none.pdf", -1, "Please choose a file to upload." },
        };

        [Theory]
        [MemberData(nameof(InvalidFiles))]
        public async Task Create_InvalidFile_Returns400(string fileName, long size, string message)
        {
            var client = await factory.CreateUserClientAsync();
            var member = await client.CreateMemberAsync("Asha");
            byte[]? file = size switch { < 0 => null, 0 => [], _ => TestApi.PdfBytes((int)size) };

            var response = await client.PostDocumentFormAsync(
                TestApi.DocumentForm("Report", member.Id, TestApi.IdentityCategory, file, fileName));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(message, await response.DetailAsync());
        }

        [Fact]
        public async Task Create_OtherUsersMember_Returns400()
        {
            var owner = await factory.CreateUserClientAsync();
            var other = await factory.CreateUserClientAsync();
            var member = await owner.CreateMemberAsync("Asha");

            var response = await other.PostDocumentFormAsync(
                TestApi.DocumentForm("Report", member.Id, TestApi.IdentityCategory, TestApi.PdfBytes()));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("Please choose a family member.", await response.DetailAsync());
        }

        [Fact]
        public async Task Create_UnknownCategory_Returns400()
        {
            var client = await factory.CreateUserClientAsync();
            var member = await client.CreateMemberAsync("Asha");

            var response = await client.PostDocumentFormAsync(TestApi.DocumentForm("Report", member.Id, Guid.NewGuid(), TestApi.PdfBytes()));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("Please choose a category.", await response.DetailAsync());
        }

        // ---------- Update (criterion 5) ----------

        [Fact]
        public async Task Update_WithoutFile_KeepsFile()
        {
            var client = await factory.CreateUserClientAsync();
            var member = await client.CreateMemberAsync("Asha");
            var doc = await client.UploadDocumentAsync(member.Id, TestApi.IdentityCategory, "Aadhaar");
            var before = await factory.GetDocumentRowAsync(doc.Id);

            var response = await client.PutDocumentFormAsync(doc.Id, TestApi.DocumentForm("Aadhaar card", member.Id, TestApi.IdentityCategory));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var updated = await response.ReadAsync<FamilyDocumentDto>();
            Assert.Equal("Aadhaar card", updated.Name);
            Assert.Equal(doc.FileName, updated.FileName);
            Assert.Equal(doc.FileSize, updated.FileSize);
            Assert.NotNull(updated.UpdatedOn);
            Assert.Equal(before.StorageKey, (await factory.GetDocumentRowAsync(doc.Id)).StorageKey);
            Assert.True(factory.StoredFileExists(before.StorageKey!));
        }

        [Fact]
        public async Task Update_WithFile_ReplacesFileAndDeletesOld()
        {
            var client = await factory.CreateUserClientAsync();
            var member = await client.CreateMemberAsync("Asha");
            var doc = await client.UploadDocumentAsync(member.Id, TestApi.IdentityCategory, "Aadhaar");
            var oldKey = (await factory.GetDocumentRowAsync(doc.Id)).StorageKey!;

            var response = await client.PutDocumentFormAsync(doc.Id,
                TestApi.DocumentForm("Aadhaar", member.Id, TestApi.IdentityCategory, TestApi.PngBytes(500), "aadhaar.png"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var updated = await response.ReadAsync<FamilyDocumentDto>();
            Assert.Equal("aadhaar.png", updated.FileName);
            Assert.Equal(500, updated.FileSize);
            Assert.Equal("png", updated.FileType);
            Assert.NotNull(updated.UpdatedOn);
            var newKey = (await factory.GetDocumentRowAsync(doc.Id)).StorageKey!;
            Assert.NotEqual(oldKey, newKey);
            Assert.False(factory.StoredFileExists(oldKey));
            Assert.True(factory.StoredFileExists(newKey));
        }

        // ---------- Ownership (criterion 6) ----------

        [Fact]
        public async Task OtherUser_AllDocumentCalls_Return404()
        {
            var owner = await factory.CreateUserClientAsync();
            var other = await factory.CreateUserClientAsync();
            var member = await owner.CreateMemberAsync("Asha");
            var otherMember = await other.CreateMemberAsync("Ravi");
            var doc = await owner.UploadDocumentAsync(member.Id, TestApi.IdentityCategory);

            var get = await other.GetAsync($"/api/documents/{doc.Id}");
            var file = await other.GetAsync($"/api/documents/{doc.Id}/file");
            var put = await other.PutDocumentFormAsync(doc.Id, TestApi.DocumentForm("Mine", otherMember.Id, TestApi.IdentityCategory));
            var delete = await other.DeleteAsync($"/api/documents/{doc.Id}");

            foreach (var response in new[] { get, file, put, delete })
            {
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                Assert.Equal("Document not found.", await response.DetailAsync());
            }
            Assert.Null((await factory.GetDocumentRowAsync(doc.Id)).DeletedOn);
        }

        // ---------- File (criterion 7) ----------

        [Fact]
        public async Task File_ReturnsStreamWithSafeHeaders()
        {
            var client = await factory.CreateUserClientAsync();
            var member = await client.CreateMemberAsync("Asha");
            var doc = await client.UploadDocumentAsync(member.Id, TestApi.IdentityCategory);

            var response = await client.GetAsync($"/api/documents/{doc.Id}/file");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
            Assert.Equal("inline", response.Content.Headers.ContentDisposition?.DispositionType);
            Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
            Assert.Equal("sandbox", response.Headers.GetValues("Content-Security-Policy").Single());
        }

        // ---------- Paging (criteria 8–12) ----------

        private async Task<(HttpClient Client, List<Guid> NewestFirst)> CreateUserWithDocumentsAsync(int count)
        {
            var client = await factory.CreateUserClientAsync();
            var member = await client.CreateMemberAsync("Asha");
            var ids = new List<Guid>();
            for (var i = 0; i < count; i++)
                ids.Add((await client.UploadDocumentAsync(member.Id, TestApi.IdentityCategory, $"Doc {i:D2}")).Id);
            ids.Reverse();
            return (client, ids);
        }

        [Fact]
        public async Task List_23Documents_PagesNewestFirst()
        {
            var (client, newestFirst) = await CreateUserWithDocumentsAsync(23);

            var first = await client.GetDocumentPageAsync();
            var second = await client.GetDocumentPageAsync("?page=2");
            var third = await client.GetDocumentPageAsync("?page=3");

            Assert.Equal((1, 10, 23, 3), (first.Page, first.PageSize, first.TotalCount, first.TotalPages));
            Assert.Equal(10, first.Items.Count);
            Assert.Equal(10, second.Items.Count);
            Assert.Equal(3, third.Items.Count);
            Assert.Equal(3, third.Page);
            Assert.Equal(newestFirst, first.Items.Concat(second.Items).Concat(third.Items).Select(d => d.Id));
        }

        [Fact]
        public async Task List_SameUploadedOn_KeepsStableOrderById()
        {
            var (client, newestFirst) = await CreateUserWithDocumentsAsync(12);
            // Do documents (page 1/2 ki seema par) ka bilkul same UploadedOn
            Guid a = newestFirst[9], b = newestFirst[10];
            await factory.ExecuteSqlAsync($"UPDATE Documents SET UploadedOn = (SELECT UploadedOn FROM Documents WHERE Id = {a}) WHERE Id = {b}");

            var runs = new List<List<Guid>>();
            for (var i = 0; i < 3; i++)
            {
                var p1 = await client.GetDocumentPageAsync();
                var p2 = await client.GetDocumentPageAsync("?page=2");
                runs.Add(p1.Items.Concat(p2.Items).Select(d => d.Id).ToList());
            }

            Assert.All(runs, r => Assert.Equal(runs[0], r));
            Assert.Equal(12, runs[0].Distinct().Count());
            // Same UploadedOn: tie-break Id desc — a baad mein bana (sequential GUID), isliye pehle aata hai
            Assert.True(runs[0].IndexOf(a) < runs[0].IndexOf(b));
        }

        [Theory]
        [InlineData("?pageSize=51", "Page size must be between 1 and 50.")]
        [InlineData("?pageSize=0", "Page size must be between 1 and 50.")]
        [InlineData("?page=0", "Page must be 1 or more.")]
        [InlineData("?page=-3", "Page must be 1 or more.")]
        public async Task List_InvalidPaging_Returns400(string query, string message)
        {
            var client = await factory.CreateUserClientAsync();

            var response = await client.GetAsync($"/api/documents{query}");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(message, await response.DetailAsync());
        }

        [Theory]
        [InlineData("?page=abc")]
        [InlineData("?pageSize=ten")]
        public async Task List_NonNumericPaging_Returns400(string query)
        {
            var client = await factory.CreateUserClientAsync();

            var response = await client.GetAsync($"/api/documents{query}");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task List_FiltersAndPaging_CountOnlyMatchingRows()
        {
            var client = await factory.CreateUserClientAsync();
            var asha = await client.CreateMemberAsync("Asha");
            var ravi = await client.CreateMemberAsync("Ravi");
            for (var i = 0; i < 4; i++)
                await client.PostDocumentFormAsync(TestApi.DocumentForm($"Report {i}", asha.Id, TestApi.MedicalCategory, TestApi.PdfBytes(), hospitalOrDoctor: "Apollo Hospital"));
            for (var i = 0; i < 3; i++)
                await client.UploadDocumentAsync(ravi.Id, TestApi.MedicalCategory, $"Apollo bill {i}");
            await client.UploadDocumentAsync(ravi.Id, TestApi.MedicalCategory, "City clinic");
            await client.UploadDocumentAsync(asha.Id, TestApi.IdentityCategory, "Apollo membership");

            var page = await client.GetDocumentPageAsync($"?categoryId={TestApi.MedicalCategory}&search=apollo&pageSize=5");

            Assert.Equal(7, page.TotalCount);
            Assert.Equal(2, page.TotalPages);
            Assert.Equal(5, page.Items.Count);
            Assert.All(page.Items, d =>
            {
                Assert.Equal(TestApi.MedicalCategory, d.CategoryId);
                Assert.True(d.Name.Contains("apollo", StringComparison.OrdinalIgnoreCase)
                    || d.HospitalOrDoctor?.Contains("apollo", StringComparison.OrdinalIgnoreCase) == true);
            });
        }

        [Fact]
        public async Task List_PagePastEnd_ReturnsEmptyItemsWithTotals()
        {
            var (client, _) = await CreateUserWithDocumentsAsync(23);

            var page = await client.GetDocumentPageAsync("?page=99");

            Assert.Empty(page.Items);
            Assert.Equal((99, 23, 3), (page.Page, page.TotalCount, page.TotalPages));
        }

        [Fact]
        public async Task List_NoDocuments_ReturnsZeroTotals()
        {
            var client = await factory.CreateUserClientAsync();

            var page = await client.GetDocumentPageAsync();

            Assert.Empty(page.Items);
            Assert.Equal((1, 10, 0, 0), (page.Page, page.PageSize, page.TotalCount, page.TotalPages));
        }

        [Fact]
        public async Task List_HugePage_DoesNotOverflow()
        {
            var client = await factory.CreateUserClientAsync();
            var member = await client.CreateMemberAsync("Asha");
            await client.UploadDocumentAsync(member.Id, TestApi.IdentityCategory);

            var page = await client.GetDocumentPageAsync($"?page={int.MaxValue}&pageSize=50");

            Assert.Empty(page.Items);
            Assert.Equal((1, 1), (page.TotalCount, page.TotalPages));
        }

        [Fact]
        public async Task List_Search101Chars_Returns400()
        {
            var client = await factory.CreateUserClientAsync();

            var response = await client.GetAsync($"/api/documents?search={new string('a', DomainRules.SearchMax + 1)}");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("Search text is too long.", await response.DetailAsync());
        }

        [Fact]
        public async Task List_Search100Chars_Returns200()
        {
            var client = await factory.CreateUserClientAsync();

            var page = await client.GetDocumentPageAsync($"?search={new string('a', DomainRules.SearchMax)}");

            Assert.Equal(0, page.TotalCount);
        }

        [Fact]
        public async Task List_RawJson_HasEnvelopeWithTotalPages()
        {
            var client = await factory.CreateUserClientAsync();

            var json = await client.GetStringAsync("/api/documents");

            Assert.StartsWith("{", json);
            foreach (var field in new[] { "\"items\"", "\"page\"", "\"pageSize\"", "\"totalCount\"", "\"totalPages\"" })
                Assert.Contains(field, json);
        }

        // ---------- Counts (criterion 16) ----------

        private sealed record Family(HttpClient Client, Guid Asha, Guid Ravi);

        /// <summary>
        /// Asha: 2 medical + 1 identity. Ravi: 1 medical + 1 medical (deleted). Mohan (deleted member): 1 medical.
        /// Doosre user ke paas bhi medical documents.
        /// </summary>
        private async Task<Family> CreateFamilyForCountsAsync()
        {
            var other = await factory.CreateUserClientAsync();
            var stranger = await other.CreateMemberAsync("Stranger");
            await other.UploadDocumentAsync(stranger.Id, TestApi.MedicalCategory);
            await other.UploadDocumentAsync(stranger.Id, TestApi.MedicalCategory);

            var client = await factory.CreateUserClientAsync();
            var asha = await client.CreateMemberAsync("Asha");
            var ravi = await client.CreateMemberAsync("Ravi");
            var mohan = await client.CreateMemberAsync("Mohan");
            await client.UploadDocumentAsync(asha.Id, TestApi.MedicalCategory);
            await client.UploadDocumentAsync(asha.Id, TestApi.MedicalCategory);
            await client.UploadDocumentAsync(asha.Id, TestApi.IdentityCategory);
            await client.UploadDocumentAsync(ravi.Id, TestApi.MedicalCategory);
            var gone = await client.UploadDocumentAsync(ravi.Id, TestApi.MedicalCategory);
            await client.UploadDocumentAsync(mohan.Id, TestApi.MedicalCategory);
            Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/documents/{gone.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/members/{mohan.Id}")).StatusCode);
            return new Family(client, asha.Id, ravi.Id);
        }

        [Fact]
        public async Task Counts_Medical_ByMemberSumsToTotal()
        {
            var f = await CreateFamilyForCountsAsync();

            var counts = await f.Client.GetFromJsonAsync<DocumentCountsDto>($"/api/documents/counts?categoryId={TestApi.MedicalCategory}");

            Assert.Equal(3, counts!.Total);
            Assert.Equal(new[] { new MemberCountDto(f.Asha, 2), new MemberCountDto(f.Ravi, 1) }.OrderBy(x => x.MemberId), counts.ByMember.OrderBy(x => x.MemberId));
            Assert.Equal(counts.Total, counts.ByMember.Sum(x => x.Count));
            Assert.Equal([new CategoryIdCountDto(TestApi.MedicalCategory, 3)], counts.ByCategory);
        }

        [Fact]
        public async Task Counts_Member_ByCategoryWithoutZeroRows()
        {
            var f = await CreateFamilyForCountsAsync();

            var counts = await f.Client.GetFromJsonAsync<DocumentCountsDto>($"/api/documents/counts?memberId={f.Asha}");

            Assert.Equal(3, counts!.Total);
            Assert.Equal([new MemberCountDto(f.Asha, 3)], counts.ByMember);
            Assert.Equal(
                new[] { new CategoryIdCountDto(TestApi.IdentityCategory, 1), new CategoryIdCountDto(TestApi.MedicalCategory, 2) }.OrderBy(x => x.CategoryId),
                counts.ByCategory.OrderBy(x => x.CategoryId));
        }

        [Fact]
        public async Task Counts_NoFilter_OnlyOwnActiveDocuments()
        {
            var f = await CreateFamilyForCountsAsync();

            var counts = await f.Client.GetFromJsonAsync<DocumentCountsDto>("/api/documents/counts");

            Assert.Equal(4, counts!.Total);
            Assert.All(counts.ByMember, x => Assert.True(x.Count > 0));
            Assert.All(counts.ByCategory, x => Assert.True(x.Count > 0));
            Assert.Equal(new[] { f.Asha, f.Ravi }.Order(), counts.ByMember.Select(x => x.MemberId).Order());
        }

        [Fact]
        public async Task Counts_NewUser_ReturnsZeroAndEmptyGroups()
        {
            var client = await factory.CreateUserClientAsync();

            var json = await client.GetStringAsync("/api/documents/counts");
            var counts = System.Text.Json.JsonSerializer.Deserialize<DocumentCountsDto>(json, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

            Assert.Equal(0, counts!.Total);
            Assert.Empty(counts.ByMember);
            Assert.Empty(counts.ByCategory);
            Assert.Contains("\"byMember\"", json);
            Assert.Contains("\"byCategory\"", json);
        }
    }
}
