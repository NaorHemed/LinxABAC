using LinxABAC;
using LinxABAC.Logic;
using LinxABAC.Queries;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;
using StackExchange.Redis;
using System.Collections.Generic;
using System.Dynamic;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

IConnectionMultiplexer redis = ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis"));
builder.Services.AddSingleton(redis);
builder.Services.AddSingleton<IRedisQueries, RedisQueries>();

builder.Services.AddScoped<IAttributesService, AttributesService>();
builder.Services.AddScoped<IUserAttribtuesService, UserAttribtuesService>(); 
builder.Services.AddScoped<IPolicyService, PolicyService>();
builder.Services.AddScoped<IResourceService, ResourceService>();
builder.Services.AddScoped<IUserAuthorizationService, UserAuthorizationService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


app.MapGet("/attributes", (IRedisQueries redisQueries) =>
{
    return Results.Ok(redisQueries.GetAttributeDefinitions());
});

app.MapPost("/attributes", (AttributeDefinitionDto request, IAttributesService attributesService) =>
{
    bool success = attributesService.CreateAttribute(request.attributeName, request.attributeType);
    if (!success)
        return Results.BadRequest();

    return Results.Ok();
});

app.MapGet("/policies/{policyName}", (string policyName, IRedisQueries redisQueries) =>
{
    var policy = redisQueries.GetPolicy(policyName);
    if (policy == null || policy.Count == 0)
        return Results.BadRequest();

    return Results.Ok(policy);
});

app.MapPost("/policies", (PolicyDefinitionDto request, IPolicyService policyService) =>
{
    bool success = policyService.CreatePolicy(request);
    if (!success)
        return Results.BadRequest();

    return Results.Ok();
});

app.MapPut("/policies/{policyName}", (string policyName, List<PolicyConditionDto> conditions, IPolicyService policyService) =>
{
    bool success = policyService.UpdatePolicy(policyName, conditions);
    if (!success)
        return Results.BadRequest();

    return Results.Ok();
});

app.MapPost("/users", (Dictionary<string, string> attributes, IUserAttribtuesService userAttribtuesService) =>
{
    Guid? userId = userAttribtuesService.CreateUser(attributes);
    if (userId == null)
        return Results.BadRequest();

    return Results.Ok(new { userId = userId });
});

app.MapGet("/users/{userId}", ([FromRoute] Guid userId, IRedisQueries redisQueries) =>
{
    var attribute = redisQueries.GetUserAttributes(userId.ToString());
    if (attribute == null)
        return Results.BadRequest();

    return Results.Json(attribute);
});

app.MapPut("/users/{userId}", ([FromRoute] Guid userId, Dictionary<string,string> attributes, IUserAttribtuesService userAttribtuesService) =>
{
    bool success = userAttribtuesService.SetUserAttributes(userId, attributes);
    if (success == false)
        return Results.BadRequest();

    return Results.Ok();
});

app.MapGet("/resources/{resourceName}", ([FromRoute] string resourceName, IRedisQueries redisQueries) =>
{
    var resourcePolicies = redisQueries.GetResourcePolicies(resourceName);
    if (resourcePolicies == null || resourcePolicies.Count == 0)
        return Results.BadRequest();

    return Results.Ok(resourcePolicies);
});

app.MapPost("/resources", (CreateResourceRequest request, IResourceService resourceService) =>
{
    bool success = resourceService.CreateResource(request);
    if (success == false)
        return Results.BadRequest();

    return Results.Ok();
});

app.MapGet("/authorize", (string resourceName, Guid userId, IUserAuthorizationService userAuthorizationService) =>
{
    bool isAuthorized = userAuthorizationService.IsAuthorized2(resourceName, userId.ToString());
    return Results.Ok(new { isAuthorized = isAuthorized });
});

app.Run();

public record AttributeDefinitionDto(string attributeName, string attributeType);
public record PolicyDefinitionDto(string policyName, List<PolicyConditionDto> conditions);
public record PolicyConditionDto(string attributeName, string @operator, string value);
public record CreateResourceRequest(string resourceName, List<string> Policies);