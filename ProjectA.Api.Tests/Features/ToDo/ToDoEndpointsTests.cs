using System.Data;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ProjectA.Api.Data;
using ProjectA.Api.Features.ToDo.GetToDoById;
using ProjectA.Api.Features.ToDo.GetToDoList;
using Xunit;

namespace ProjectA.Api.Tests.Features.ToDo;

[Collection(nameof(ApiCollection))]
public class ToDoEndpointsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client;

    public ToDoEndpointsTests(ApiFactory factory)
    {
        _client = factory.CreateAuthenticatedClient();

        var connectionFactory = factory.Services.GetService<IDbConnectionFactory>();
        if (connectionFactory != null)
        {
            ((PostgresDbConnectionFactory)connectionFactory).SeedToDos();
        }
    }

    [Fact]
    public async Task GetList_WithoutIncludeDone_ReturnsOnlyOutstandingItems()
    {
        var response = await _client.GetAsync("/api/todo");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var todos = await response.Content
            .ReadFromJsonAsync<List<GetToDoListEndpoint.ToDoListItemResponse>>(JsonOptions);
        Assert.NotNull(todos);
        Assert.Equal(2, todos.Count);
        Assert.All(todos, todo => Assert.False(todo.Done));
    }

    [Fact]
    public async Task GetList_WithIncludeDone_ReturnsAllItems()
    {
        var response = await _client.GetAsync("/api/todo?includeDone=true");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var todos = await response.Content
            .ReadFromJsonAsync<List<GetToDoListEndpoint.ToDoListItemResponse>>(JsonOptions);
        Assert.NotNull(todos);
        Assert.Equal(3, todos.Count);
        Assert.Contains(todos, todo => todo.Done);
    }

    [Fact]
    public async Task GetById_WhenExists_ReturnsItem()
    {
        // as the Id is auto incrementing, we cant assume a key - so get the first 'in progress' item
        // and use that Id to test the read by Id
        var response = await _client.GetAsync("/api/todo?includeDone=true");
        var todos = await response.Content
            .ReadFromJsonAsync<List<GetToDoListEndpoint.ToDoListItemResponse>>(JsonOptions);

        var firstInProgressItem = todos!.First(a => a.Done == false);

        var responseItem = await _client.GetAsync($"/api/todo/{firstInProgressItem.Id}");

        Assert.Equal(HttpStatusCode.OK, responseItem.StatusCode);

        var todo = await responseItem.Content
            .ReadFromJsonAsync<GetToDoByIdEndpoint.ToDoResponse>(JsonOptions);
        Assert.NotNull(todo);
        Assert.Equal(firstInProgressItem.Id, todo.Id);
        Assert.Equal(firstInProgressItem.Title, todo.Title);
    }

    [Fact]
    public async Task GetById_WhenExists_ReturnsCompletedItem()
    {
        // as the Id is auto incrementing, we cant assume a key - so get the first done item
        // and use that Id to test the read by Id
        var response = await _client.GetAsync("/api/todo?includeDone=true");
        var todos = await response.Content
            .ReadFromJsonAsync<List<GetToDoListEndpoint.ToDoListItemResponse>>(JsonOptions);
        var firstDoneItem = todos!.First(a => a.Done);

        var responseItem = await _client.GetAsync($"/api/todo/{firstDoneItem.Id}");

        Assert.Equal(HttpStatusCode.OK, responseItem.StatusCode);

        var todo = await responseItem.Content
            .ReadFromJsonAsync<GetToDoByIdEndpoint.ToDoResponse>(JsonOptions);
        Assert.NotNull(todo);
        Assert.True(todo.Done);
    }

    [Fact]
    public async Task GetById_WhenMissing_ReturnsProblemDetails()
    {
        var response = await _client.GetAsync("/api/todo/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>(JsonOptions);
        Assert.NotNull(problem);
        Assert.Equal(404, problem.Status);
        Assert.Equal("ToDo not found", problem.Title);
        Assert.Equal("No ToDo exists with id 999.", problem.Detail);
    }

    private sealed record ProblemDetailsResponse(
        string? Type,
        string? Title,
        int? Status,
        string? Detail,
        string? Instance);
}

public static class SeedToDoTestDataExtension
{
    public static void SeedToDos(this PostgresDbConnectionFactory factory)
    {
        var connection = factory.CreateConnectionAsync().Result;
        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        // clear any remaining test data
        connection.Execute("DELETE FROM todos WHERE category = 'Test Category';");

        var toDoInsert = "INSERT INTO todos (title, actioned, category, description, date_created, date_modified)";
        toDoInsert += "VALUES";

        var todoOne = "('Test 1 ToDo',false,'Test Category','Test 1 ToDo Description','2026-01-01 10:30:00.000 +0000',null) ";
        var todoTwo = "('Test 2 ToDo',false,'Test Category','Test 2 ToDo Description','2026-02-02 10:30:00.000 +0000','2026-02-02 11:30:00.000 +0000');";
        var todoThree = "('Test 3 ToDo',true,'Test Category','Test 3 ToDo Description','2026-03-03 10:30:00.000 +0000','2026-03-03 10:30:00.000 +0000');";

        connection.Execute($"{toDoInsert} {todoOne}");
        connection.Execute($"{toDoInsert} {todoTwo}");
        connection.Execute($"{toDoInsert} {todoThree}");
    }
}
