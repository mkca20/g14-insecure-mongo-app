using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using InsecureMongoApp.API.Handlers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "InsecureMongoApp API",
        Version = "v1",
        Description = "An intentionally insecure API using MongoDB and JWT with role-based access control."
    });

    var jwtScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Authorization header. Example: Bearer {token}"
    };

    options.AddSecurityDefinition("Bearer", jwtScheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { jwtScheme, new string[] { } }
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "InsecureMongoApp API V1");
    options.RoutePrefix = "";
});

/// GET /items (reader, writer, admin required)
app.MapGet("/items", (HttpRequest req) =>
{
    var authHeader = req.Headers["Authorization"].ToString();
    if (!authHeader.StartsWith("Bearer ")) return Results.Unauthorized();

    var principal = AuthHandler.ValidateToken(authHeader[7..]);
    var role = principal != null ? AuthHandler.GetUserRoleFromPrincipal(principal) : null;

    if (role is not ("admin" or "writer" or "reader")) return Results.Unauthorized();

    var query = new Dictionary<string, string>();
    if (req.Query.TryGetValue("id", out var id))
        query["id"] = id!;

    var (statusCode, body) = InsecureHandler.HandleGet(query);
    return Results.Json(body, statusCode: statusCode);
})
.WithOpenApi(op =>
{
    op.Summary = "Retrieve an item by ID (requires reader+)";
    op.Description = "Authenticated endpoint to fetch item(s) from MongoDB based on a query parameter `id`.";
    op.Parameters.Add(new OpenApiParameter
    {
        Name = "id",
        In = ParameterLocation.Query,
        Required = true,
        Description = "Parsed ID",
        Schema = new OpenApiSchema { Type = "string" }
    });
    op.Responses["200"] = new OpenApiResponse { Description = "Item(s) retrieved successfully" };
    op.Responses["404"] = new OpenApiResponse { Description = "No items found" };
    op.Responses["400"] = new OpenApiResponse { Description = "Invalid query" };
    op.Responses["401"] = new OpenApiResponse { Description = "Unauthorized (missing or insufficient role)" };
    return op;
});

/// POST /items (writer or admin)
app.MapPost("/items", async (HttpRequest req) =>
{
    var authHeader = req.Headers["Authorization"].ToString();
    if (!authHeader.StartsWith("Bearer ")) return Results.Unauthorized();

    var principal = AuthHandler.ValidateToken(authHeader[7..]);
    var role = principal != null ? AuthHandler.GetUserRoleFromPrincipal(principal) : null;

    if (role is not ("admin" or "writer")) return Results.Unauthorized();

    using var reader = new StreamReader(req.Body);
    var body = await reader.ReadToEndAsync();

    var (statusCode, item) = InsecureHandler.HandlePost(body);
    return Results.Json(item, statusCode: statusCode);
})
.WithOpenApi(op =>
{
    op.Summary = "Insert a new item (requires writer+)";
    op.Description = "Parses the request body and stores the item in MongoDB. Signs item ID with RSA key. Requires writer or admin.";
    op.RequestBody = new OpenApiRequestBody
    {
        Required = true,
        Content = {
            ["application/json"] = new OpenApiMediaType
            {
                Schema = new OpenApiSchema
                {
                    Type = "object",
                    Description = "JSON object to insert into MongoDB"
                }
            }
        }
    };
    op.Responses["201"] = new OpenApiResponse { Description = "Item created" };
    op.Responses["400"] = new OpenApiResponse { Description = "Invalid request body" };
    op.Responses["401"] = new OpenApiResponse { Description = "Unauthorized (JWT missing/invalid or role insufficient)" };
    return op;
});

/// POST /auth/token (login)
app.MapPost("/auth/token", async (HttpRequest req) =>
{
    using var reader = new StreamReader(req.Body);
    var body = await reader.ReadToEndAsync();
    var data = JsonSerializer.Deserialize<Dictionary<string, string>>(body);

    var username = data?["username"];
    var password = data?["password"];

    if (username != null && password != null && UserService.ValidateUser(username, password))
    {
        var role = UserService.GetUserRole(username) ?? "reader";
        var token = AuthHandler.GenerateToken(username, role);
        return Results.Json(new { token });
    }

    return Results.Unauthorized();
})
.WithOpenApi(op =>
{
    op.Summary = "Authenticate and get a JWT";
    op.Description = "Use a username and password to obtain a JWT token. Passwords are hashed.";
    op.RequestBody = new OpenApiRequestBody
    {
        Required = true,
        Content = {
            ["application/json"] = new OpenApiMediaType
            {
                Schema = new OpenApiSchema
                {
                    Type = "object",
                    Properties =
                    {
                        ["username"] = new OpenApiSchema { Type = "string" },
                        ["password"] = new OpenApiSchema { Type = "string", Format = "password" }
                    }
                }
            }
        }
    };
    op.Responses["200"] = new OpenApiResponse { Description = "JWT returned" };
    op.Responses["401"] = new OpenApiResponse { Description = "Invalid credentials" };
    return op;
});

/// POST /auth/register (admin-only)
app.MapPost("/auth/register", async (HttpRequest req) =>
{
    var authHeader = req.Headers["Authorization"].ToString();
    var principal = authHeader.StartsWith("Bearer ")
        ? AuthHandler.ValidateToken(authHeader[7..])
        : null;

    if (principal?.Identity?.Name != "admin") return Results.Unauthorized();

    using var reader = new StreamReader(req.Body);
    var body = await reader.ReadToEndAsync();
    var data = JsonSerializer.Deserialize<Dictionary<string, string>>(body);

    var newUser = data?["username"];
    var newPassword = data?["password"];
    var newRole = data?.TryGetValue("role", out var r) == true && (r == "reader" || r == "writer") ? r : "reader";

    if (string.IsNullOrWhiteSpace(newUser) || string.IsNullOrWhiteSpace(newPassword))
        return Results.BadRequest();

    if (UserService.CreateUser(newUser, newPassword, newRole))
        return Results.Ok(new { message = "User created" });

    return Results.Conflict(new { error = "User already exists" });
})
.WithOpenApi(op =>
{
    op.Summary = "Register a new user (admin only)";
    op.Description = "Allows the admin user to create new users with hashed passwords and roles (`reader`, `writer`).";
    op.RequestBody = new OpenApiRequestBody
    {
        Required = true,
        Content = {
            ["application/json"] = new OpenApiMediaType
            {
                Schema = new OpenApiSchema
                {
                    Type = "object",
                    Properties =
                    {
                        ["username"] = new OpenApiSchema { Type = "string" },
                        ["password"] = new OpenApiSchema { Type = "string", Format = "password" },
                        ["role"] = new OpenApiSchema
                        {
                            Type = "string",
                            Enum = new List<IOpenApiAny>
                            {
                                new OpenApiString("reader"),
                                new OpenApiString("writer")
                            },
                            Description = "Must be 'reader' or 'writer'. Only admin can create users."
                        }
                    }
                }
            }
        }
    };
    op.Responses["200"] = new OpenApiResponse { Description = "User created successfully" };
    op.Responses["401"] = new OpenApiResponse { Description = "Unauthorized (must be admin)" };
    op.Responses["400"] = new OpenApiResponse { Description = "Missing fields" };
    op.Responses["409"] = new OpenApiResponse { Description = "User already exists" };
    return op;
});

app.Run();

public partial class Program { }
