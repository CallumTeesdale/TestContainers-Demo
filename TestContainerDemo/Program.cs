using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = Directory.GetCurrentDirectory()
});

var config = builder.Configuration;
builder.Services.AddSingleton<IDbConnectionFactory>(_ =>
    new SqlConnectionFactory(config.GetValue<string>("Database:ConnectionString")));
builder.Services.AddSingleton<DatabaseInitializer>();
//builder.Services.AddDbContext<TodoDb>();
var app = builder.Build();
app.MapGet("/todoitems",
    async (IDbConnectionFactory db) =>
    {
        using var dbConnection = await db.CreateConnectionAsync();
        var items = await dbConnection.QueryAsync<Todo>("SELECT * FROM Todo");
        return items.Select(x => new TodoItemDTO(x)).ToList();
    });
app.MapGet("/todoitems/{id}",
    async (int id, IDbConnectionFactory db) =>
    {
        using var dbConnection = await db.CreateConnectionAsync();
        var todo = await dbConnection.QueryFirstOrDefaultAsync<Todo>(
                       "SELECT * FROM TodoItems WHERE Id = @id", new
                       {
                           id
                       });
        return todo is not null ? Results.Ok(new TodoItemDTO(todo)) : Results.NotFound();
    });
app.MapPost("/todoitems",
    async (TodoItemDTO todoItemDTO, IDbConnectionFactory db) =>
    {
        var todoItem = new Todo
        {
            IsComplete = todoItemDTO.IsComplete,
            ItemName = todoItemDTO.ItemName
        };

        using var dbConnection = await db.CreateConnectionAsync();
        await dbConnection.ExecuteAsync(
            "INSERT INTO Todo (IsComplete, ItemName) VALUES (@IsComplete, @ItemName)",
            todoItem);
        return Results.Created($"/todoitems/{todoItem.Id}", new TodoItemDTO(todoItem));
    });
app.MapPut("/todoitems/{id}",
    async (int id, TodoItemDTO todoItemDTO, IDbConnectionFactory db) =>
    {
        using var dbConnection = await db.CreateConnectionAsync();
        var todoItem = await dbConnection.QueryFirstOrDefaultAsync<Todo>(
                           "SELECT * FROM TodoItems WHERE Id = @id", new
                           {
                               id
                           });
        if (todoItem is null)
        {
            return Results.NotFound();
        }

        todoItem.IsComplete = todoItemDTO.IsComplete;
        todoItem.ItemName = todoItemDTO.ItemName;
        await dbConnection.ExecuteAsync(
            "UPDATE TodoItems SET IsComplete = @IsComplete, Name = @Name WHERE Id = @Id",
            todoItem);
        return Results.Ok(new TodoItemDTO(todoItem));
    });
app.MapDelete("/todoitems/{id}",
    async (int id, IDbConnectionFactory db) =>
    {
        using var dbConnection = await db.CreateConnectionAsync();
        var todoItem = await dbConnection.QueryFirstOrDefaultAsync<Todo>(
                           "SELECT * FROM TodoItems WHERE Id = @id", new
                           {
                               id
                           });
        if (todoItem is null)
        {
            return Results.NotFound();
        }

        await dbConnection.ExecuteAsync(
            "DELETE FROM TodoItems WHERE Id = @Id",
            new
            {
                id
            });
        return Results.NoContent();
    });

var databaseInitializer = app.Services.GetRequiredService<DatabaseInitializer>();
await databaseInitializer.InitializeAsync();
app.Run();

public class Todo
{
    public int Id { get; set; }
    public string? ItemName { get; set; }
    public bool IsComplete { get; set; }
    public string? Secret { get; set; }
}

public class TodoItemDTO
{
    public int Id { get; set; }
    public string? ItemName { get; set; }
    public bool IsComplete { get; set; }

    public TodoItemDTO()
    {
    }

    public TodoItemDTO(Todo todoItem) =>
        (Id, ItemName, IsComplete) = (todoItem.Id, todoItem.ItemName, todoItem.IsComplete);
}


public interface IDbConnectionFactory
{
    public Task<IDbConnection> CreateConnectionAsync();
}

public class SqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<IDbConnection> CreateConnectionAsync()
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }
}

public class TodoDb : DbContext
{
    public TodoDb(DbContextOptions<TodoDb> options)
        : base(options)
    {
    }

    public DbSet<Todo> Todos => Set<Todo>();
}

public class DatabaseInitializer
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DatabaseInitializer(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task InitializeAsync()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        await connection.ExecuteAsync(@"
            CREATE TABLE Todo (
                Id INT PRIMARY KEY IDENTITY,
                ItemName NVARCHAR(255) NOT NULL,
                IsComplete BIT NOT NULL
            )");
    }
}

public partial class Program
{
}