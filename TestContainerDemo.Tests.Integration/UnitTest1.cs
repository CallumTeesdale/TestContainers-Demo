using System.Net;
using System.Net.Http.Json;
using System.Text;
using Bogus;
using FluentAssertions;
using Newtonsoft.Json;

namespace TestContainerDemo.Tests.Integration;

public class GetToDosTests : IClassFixture<ToDoApiFactory>
{
    private readonly HttpClient _client;

    private readonly Faker<TodoItemDTO> _fakeRequestGenerator = new Faker<TodoItemDTO>()
                                                                .RuleFor(x => x.ItemName, f => f.Lorem.Word())
                                                                .RuleFor(x => x.IsComplete, f => f.Random.Bool());

    public GetToDosTests(ToDoApiFactory apiFactory)
    {
        _client = apiFactory.CreateClient();
    }

    [Fact]
    public async Task Get()
    {
        // Arrange
        var todo = _fakeRequestGenerator.Generate();
        var createResponse = await _client.PostAsJsonAsync("/todoitems", todo);
        var createResponseContent = await createResponse.Content.ReadFromJsonAsync<TodoItemDTO>();

        // Act
        var getResponse = await _client.GetAsync("/todoitems");

        // Assert
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var getResponseContent = await getResponse.Content.ReadFromJsonAsync<List<TodoItemDTO>>();
        getResponseContent!.First().Should().BeEquivalentTo(createResponseContent, options =>
        {
            options.Excluding(x => x.Id);
            return options;
        });
    }
}