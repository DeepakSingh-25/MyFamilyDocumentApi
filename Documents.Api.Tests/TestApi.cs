using System.Net.Http.Json;
using Documents.Core.Models;
using Documents.Core.Rules;
using Microsoft.AspNetCore.Mvc;

namespace Documents.Api.Tests
{
    /// <summary>Chhote HTTP helpers taaki tests sirf behaviour dikhayein.</summary>
    internal static class TestApi
    {
        /// <summary>Medical Documents (DomainRules.MedicalCategoryId).</summary>
        public static readonly Guid MedicalCategory = DomainRules.MedicalCategoryId;
        public static readonly Guid IdentityCategory = DomainRules.SystemCategoryId(1);

        /// <summary>App:TimeZone ka default — API "today" isi se nikalti hai.</summary>
        public static DateOnly TodayIst() =>
            DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "India Standard Time"));

        /// <summary>System relationship ka id (seed: DomainRules.SystemRelationships, id = SystemRelationshipId(index + 1)).</summary>
        public static Guid RelationshipId(string systemName)
        {
            var index = DomainRules.SystemRelationships.ToList().IndexOf(systemName);
            return index >= 0 ? DomainRules.SystemRelationshipId(index + 1) : throw new ArgumentException($"Not a system relationship: {systemName}");
        }

        public static Task<HttpResponseMessage> PostMemberAsync(this HttpClient client, string name, string relationship, DateOnly? dob = null) =>
            client.PostMemberAsync(name, RelationshipId(relationship), dob);

        public static Task<HttpResponseMessage> PostMemberAsync(this HttpClient client, string name, Guid relationshipId, DateOnly? dob = null) =>
            client.PostAsJsonAsync("/api/members", new { name, relationshipId, dateOfBirth = dob });

        public static Task<HttpResponseMessage> PutMemberAsync(this HttpClient client, Guid id, string name, string relationship, DateOnly? dob = null) =>
            client.PutMemberAsync(id, name, RelationshipId(relationship), dob);

        public static Task<HttpResponseMessage> PutMemberAsync(this HttpClient client, Guid id, string name, Guid relationshipId, DateOnly? dob = null) =>
            client.PutAsJsonAsync($"/api/members/{id}", new { name, relationshipId, dateOfBirth = dob });

        public static async Task<FamilyMemberDto> CreateMemberAsync(this HttpClient client, string name, string relationship = "Other")
        {
            var response = await client.PostMemberAsync(name, relationship);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<FamilyMemberDto>())!;
        }

        /// <summary>Chhoti si valid PDF (magic bytes %PDF) upload karta hai.</summary>
        public static async Task<FamilyDocumentDto> UploadDocumentAsync(this HttpClient client, Guid memberId, Guid categoryId, string name = "Report")
        {
            using var form = new MultipartFormDataContent
            {
                { new StringContent(name), "name" },
                { new StringContent(memberId.ToString()), "memberId" },
                { new StringContent(categoryId.ToString()), "categoryId" },
            };
            var file = new ByteArrayContent("%PDF-1.4\n%%EOF\n"u8.ToArray());
            file.Headers.ContentType = new("application/pdf");
            form.Add(file, "file", "report.pdf");

            var response = await client.PostAsync("/api/documents", form);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<FamilyDocumentDto>())!;
        }

        public static async Task<HttpResponseMessage> PostDocumentAsync(this HttpClient client, Guid memberId)
        {
            using var form = new MultipartFormDataContent
            {
                { new StringContent("Late upload"), "name" },
                { new StringContent(memberId.ToString()), "memberId" },
                { new StringContent(IdentityCategory.ToString()), "categoryId" },
            };
            var file = new ByteArrayContent("%PDF-1.4\n%%EOF\n"u8.ToArray());
            file.Headers.ContentType = new("application/pdf");
            form.Add(file, "file", "late.pdf");
            return await client.PostAsync("/api/documents", form);
        }

        /// <summary>Asli PDF header + padding (FileRules magic bytes %PDF).</summary>
        public static byte[] PdfBytes(int size = 64)
        {
            var bytes = new byte[Math.Max(size, 8)];
            "%PDF-1.4\n"u8.CopyTo(bytes);
            return bytes;
        }

        /// <summary>Asli PNG header + padding.</summary>
        public static byte[] PngBytes(int size = 64)
        {
            var bytes = new byte[Math.Max(size, 8)];
            new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(bytes, 0);
            return bytes;
        }

        /// <summary>Poora multipart form — fields null ho to nahi bheje jaate; file null = file nahi.</summary>
        public static MultipartFormDataContent DocumentForm(
            string? name, Guid memberId, Guid categoryId, byte[]? file = null, string fileName = "report.pdf",
            string? hospitalOrDoctor = null, string? notes = null)
        {
            var form = new MultipartFormDataContent
            {
                { new StringContent(memberId.ToString()), "memberId" },
                { new StringContent(categoryId.ToString()), "categoryId" },
            };
            if (name is not null) form.Add(new StringContent(name), "name");
            if (hospitalOrDoctor is not null) form.Add(new StringContent(hospitalOrDoctor), "hospitalOrDoctor");
            if (notes is not null) form.Add(new StringContent(notes), "notes");
            if (file is not null)
            {
                var content = new ByteArrayContent(file);
                content.Headers.ContentType = new("application/octet-stream");
                form.Add(content, "file", fileName);
            }
            return form;
        }

        public static async Task<HttpResponseMessage> PostDocumentFormAsync(this HttpClient client, MultipartFormDataContent form)
        {
            using (form) return await client.PostAsync("/api/documents", form);
        }

        public static async Task<HttpResponseMessage> PutDocumentFormAsync(this HttpClient client, Guid id, MultipartFormDataContent form)
        {
            using (form) return await client.PutAsync($"/api/documents/{id}", form);
        }

        /// <summary>GET /api/documents{query} — envelope (spec 009 §B.1). Success hona chahiye.</summary>
        public static async Task<DocumentPage> GetDocumentPageAsync(this HttpClient client, string query = "")
        {
            var response = await client.GetAsync($"/api/documents{query}");
            Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<DocumentPage>())!;
        }

        public static async Task<T> ReadAsync<T>(this HttpResponseMessage response) =>
            (await response.Content.ReadFromJsonAsync<T>())!;

        public static async Task<string?> DetailAsync(this HttpResponseMessage response) =>
            (await response.Content.ReadFromJsonAsync<ProblemDetails>())?.Detail;
    }
}
