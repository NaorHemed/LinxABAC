using LinxABAC;
using LinxABAC.Database;
using LinxABAC.Logic;
using LinxABAC.Models.AbacPermissions;
using LinxABAC.Queries;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;
using StackExchange.Redis;
using System.Dynamic;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("SqlServer")));

IConnectionMultiplexer redis = ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis"));
builder.Services.AddSingleton(redis);
builder.Services.AddSingleton<IRedisQueries, RedisQueries>();
builder.Services.AddScoped<IUserAttribtuesService, UserAttribtuesService>(); 
builder.Services.AddScoped<IPolicyService, PolicyService>(); 

//build the database when app starts
builder.Services.AddHostedService<DatabaseWarmup>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


app.MapGet("/attributes", async (AppDbContext dbContext) =>
{
    var attributes = await dbContext.Attributes.Select(a => new AttributeDefinitionDto(a.AttributeName, a.AttributeType))
        .ToListAsync();

    return Results.Ok(attributes);
});

app.MapPost("/attributes", async (AttributeDefinitionDto request, AppDbContext dbContext) =>
{
    if (request.attributeType != Constants.IntegerAttribute &&
    request.attributeType != Constants.StringAttribute &&
    request.attributeType != Constants.BooleanAttribute)
    {
        return Results.BadRequest("Invalid attribute type");
    }

    if ((await dbContext.Attributes.CountAsync()) > Constants.MaxAttributes)
        return Results.BadRequest("Too many attributes");

    if (await dbContext.Attributes.AnyAsync(a => a.AttributeName == request.attributeName))
        return Results.BadRequest("Attribute already exists");


    await dbContext.Attributes.AddAsync(new AttributeDefinition
    {
        AttributeName = request.attributeName,
        AttributeType = request.attributeType
    });

    await dbContext.SaveChangesAsync();
    return Results.Ok();
});

app.MapPost("/policies", async (PolicyDefinitionDto request, IPolicyService policyService) =>
{
    string? errorMsg = await policyService.CreatePolicyAsync(request);
    if (errorMsg != null)
        return Results.BadRequest(errorMsg);

    return Results.Ok();
});

app.MapPost("/users", async (Dictionary<string,string> attributes, AppDbContext dbContext, IUserAttribtuesService userAttribtuesService) =>
{
    using (var t = await dbContext.Database.BeginTransactionAsync())
    {
        Guid userId = Guid.NewGuid();
        await dbContext.Users.AddAsync(new User { UserId = userId }); //store to database

        string? errorMsg = await userAttribtuesService.SetUserAttributesAsync(userId, attributes); //set attributes in redis

        if (errorMsg == null)
        {
            await dbContext.SaveChangesAsync();
            await t.CommitAsync();
            return Results.Ok(new { userId = userId });
        }
        else
        {
            t.Rollback();
            return Results.BadRequest(errorMsg);
        }
    }
});

app.MapGet("/users/{userId}", ([FromRoute]Guid userId, IRedisQueries redisQueries) =>
{
    return Results.Ok(redisQueries.GetUserAttributes(userId.ToString()));
});

app.MapPut("/users", async (UpdateUserAttributesRequest request, IUserAttribtuesService userAttribtuesService) =>
{
    string? errorMsg = await userAttribtuesService.SetUserAttributesAsync(request.userId, request.attributes);
    if (errorMsg != null)
        return Results.BadRequest(errorMsg);

    return Results.Ok();
});

app.Run();

public record AttributeDefinitionDto(string attributeName, string attributeType);
public record PolicyDefinitionDto(string policyName, List<PolicyConditionDto> conditions);
public record PolicyConditionDto(string attributeName, string @operator, string value);
public record UpdateUserAttributesRequest(Guid userId, Dictionary<string, string> attributes);