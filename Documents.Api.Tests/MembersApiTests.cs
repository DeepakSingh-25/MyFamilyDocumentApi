using System.Net;
using System.Net.Http.Json;
using Documents.Core.Models;
using Documents.Core.Rules;

namespace Documents.Api.Tests
{
    /// <summary>Spec 007 §B.9 criteria 2–12: members CRUD, validation, ownership.</summary>
    [Collection(ApiCollection.Name)]
    public sealed class MembersApiTests(ApiFactory factory)
    {
        [Theory]
        [InlineData("GET", "/api/members")]
        [InlineData("GET", "/api/members/3f2504e0-4f89-11d3-9a0c-0305e82c3301")]
        [InlineData("POST", "/api/members")]
        [InlineData("PUT", "/api/members/3f2504e0-4f89-11d3-9a0c-0305e82c3301")]
        [InlineData("DELETE", "/api/members/3f2504e0-4f89-11d3-9a0c-0305e82c3301")]
        public async Task Members_WithoutToken_Returns401(string method, string url)
        {
            var client = factory.CreateClient();

            var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), url)
            {
                Content = method is "POST" or "PUT" ? JsonContent.Create(new { name = "A", relationshipId = DomainRules.SelfRelationshipId }) : null,
            });

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Create_ValidRequest_Returns201WithTrimmedName()
        {
            var client = await factory.CreateUserClientAsync();

            var response = await client.PostMemberAsync("  Asha  ", "Mother");

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.NotNull(response.Headers.Location);
            var member = await response.ReadAsync<FamilyMemberDto>();
            Assert.Equal("Asha", member.Name);
            Assert.Equal(0, member.DocumentCount);
            Assert.Null(member.DateOfBirth);
        }

        [Fact]
        public async Task Create_SameNameDifferentCase_Returns409()
        {
            var client = await factory.CreateUserClientAsync();
            await client.CreateMemberAsync("Asha");

            var response = await client.PostMemberAsync("asha", "Mother");

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("A family member with this name already exists.", await response.DetailAsync());
        }

        [Fact]
        public async Task Update_ToOtherMembersName_Returns409()
        {
            var client = await factory.CreateUserClientAsync();
            await client.CreateMemberAsync("Asha");
            var ravi = await client.CreateMemberAsync("Ravi");

            var response = await client.PutMemberAsync(ravi.Id, "ASHA", "Son");

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("A family member with this name already exists.", await response.DetailAsync());
        }

        [Fact]
        public async Task Update_OwnSameName_Returns200()
        {
            var client = await factory.CreateUserClientAsync();
            var asha = await client.CreateMemberAsync("Asha", "Mother");

            var response = await client.PutMemberAsync(asha.Id, "Asha", "Grandmother");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("Grandmother", (await response.ReadAsync<FamilyMemberDto>()).Relationship);
        }

        [Fact]
        public async Task Update_MissingId_Returns404()
        {
            var client = await factory.CreateUserClientAsync();

            var response = await client.PutMemberAsync(Guid.NewGuid(), "Nobody", "Other");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("Family member not found.", await response.DetailAsync());
        }

        public static TheoryData<object, string> InvalidBodies => new()
        {
            { new { name = "   ", relationshipId = DomainRules.SystemRelationshipId(4) }, "Name is required." },
            { new { name = new string('a', 101), relationshipId = DomainRules.SystemRelationshipId(4) }, "Name is too long." },
            { new { name = "Asha", relationshipId = Guid.NewGuid() }, "Please choose a relationship." },
            { new { name = "Asha" }, "Please choose a relationship." },
            { new { name = "Asha", relationshipId = DomainRules.SystemRelationshipId(4), dateOfBirth = "1899-12-31" }, "Please enter a valid date of birth." },
        };

        [Theory]
        [MemberData(nameof(InvalidBodies))]
        public async Task Create_InvalidBody_Returns400WithMessage(object body, string expected)
        {
            var client = await factory.CreateUserClientAsync();

            var response = await client.PostAsJsonAsync("/api/members", body);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(expected, await response.DetailAsync());
        }

        [Fact]
        public async Task Create_DateOfBirthTomorrow_Returns400()
        {
            var client = await factory.CreateUserClientAsync();

            var response = await client.PostMemberAsync("Baby", "Daughter", TestApi.TodayIst().AddDays(1));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("Date of birth cannot be in the future.", await response.DetailAsync());
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task Create_DateOfBirthTodayOr1900_Returns201(int which)
        {
            var client = await factory.CreateUserClientAsync();
            var dob = which == 0 ? TestApi.TodayIst() : new DateOnly(1900, 1, 1);

            var response = await client.PostMemberAsync("Born", "Son", dob);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.Equal(dob, (await response.ReadAsync<FamilyMemberDto>()).DateOfBirth);
        }

        [Fact]
        public async Task OtherUsersMember_GetPutDelete_Return404AndDataUnchanged()
        {
            var userA = await factory.CreateUserClientAsync();
            var userB = await factory.CreateUserClientAsync();
            var asha = await userA.CreateMemberAsync("Asha", "Mother");

            var get = await userB.GetAsync($"/api/members/{asha.Id}");
            var put = await userB.PutMemberAsync(asha.Id, "Hacked", "Other");
            var delete = await userB.DeleteAsync($"/api/members/{asha.Id}");

            Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, put.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
            var still = await userA.GetFromJsonAsync<FamilyMemberDto>($"/api/members/{asha.Id}");
            Assert.Equal("Asha", still!.Name);
        }

        [Fact]
        public async Task List_ReturnsOnlyOwnMembersInCreatedOrderWithDocumentCount()
        {
            var userA = await factory.CreateUserClientAsync();
            var userB = await factory.CreateUserClientAsync();
            var first = await userA.CreateMemberAsync("First");
            var second = await userA.CreateMemberAsync("Second");
            await userB.CreateMemberAsync("Other user");
            await userA.UploadDocumentAsync(second.Id, TestApi.IdentityCategory);

            var list = await userA.GetFromJsonAsync<List<FamilyMemberDto>>("/api/members");

            Assert.Equal([first.Id, second.Id], list!.Select(m => m.Id));
            Assert.Equal([0, 1], list!.Select(m => m.DocumentCount));
        }

        [Fact]
        public async Task Create_SecondSelf_Returns409()
        {
            var client = await factory.CreateUserClientAsync();
            await client.CreateMemberAsync("Me", "Self");

            var response = await client.PostMemberAsync("Also me", "Self");

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("Only one member can be 'Self'.", await response.DetailAsync());
        }

        [Fact]
        public async Task Update_OtherUsersMemberToSelf_Returns404Not409()
        {
            var userA = await factory.CreateUserClientAsync();
            var userB = await factory.CreateUserClientAsync();
            var asha = await userA.CreateMemberAsync("Asha", "Mother");
            await userB.CreateMemberAsync("Me", "Self");

            var response = await userB.PutMemberAsync(asha.Id, "Asha", "Self");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Update_ExistingSelfName_Returns200()
        {
            var client = await factory.CreateUserClientAsync();
            var me = await client.CreateMemberAsync("Me", "Self");

            var response = await client.PutMemberAsync(me.Id, "Me Renamed", "Self");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Update_MoveSelfToAnotherMember_Returns200()
        {
            var client = await factory.CreateUserClientAsync();
            var me = await client.CreateMemberAsync("Me", "Self");
            var dad = await client.CreateMemberAsync("Dad", "Son");

            var first = await client.PutMemberAsync(me.Id, "Me", "Father");
            var second = await client.PutMemberAsync(dad.Id, "Dad", "Self");

            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        }
    }
}
