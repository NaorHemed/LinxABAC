using LinxABAC;
using LinxABAC.Database;
using LinxABAC.Models.AbacPermissions;
using LinxABAC.Queries;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("SqlServer")));

IConnectionMultiplexer redis = ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis"));
builder.Services.AddSingleton(redis);
builder.Services.AddSingleton<IComputedResultsQueries>();


//build the database when app starts
builder.Services.AddHostedService<DatabaseWarmup>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


app.MapGet("/attributes", async ([FromServices] AppDbContext dbContext) =>
{
    var attributes = await dbContext.Attributes.Select(a => new AttributeDefinitionDto(a.AttributeName, a.AttributeType))
        .ToListAsync();

    return Results.Ok(attributes);
});

app.MapPost("/attributes", async ([FromBody] AttributeDefinitionDto request, [FromServices] AppDbContext dbContext) =>
{
    if (request.attributeType != "boolean" &&
    request.attributeType != "string" &&
    request.attributeType != "integer")
    {
        return Results.BadRequest("Invalid attribute type");
    }

    if ((await dbContext.Attributes.CountAsync()) > Constants.MaxAttributes)
        return Results.BadRequest("Too many attributes");

    await dbContext.Attributes.AddAsync(new AttributeDefinition
    {
        AttributeName = request.attributeName,
        AttributeType = request.attributeType
    });

    await dbContext.SaveChangesAsync();
    return Results.Ok();
});

app.MapPost("/policies", async ([FromBody] PolicyDefinitionDto request, [FromServices] AppDbContext dbContext) =>
{
    //check count of policies in the request
    if (request.conditions.Count > Constants.MaxConditionsPerPolicy)
        return Results.BadRequest("Too many conditions for policy");

    //check valid operator on all condiions
    bool allConditionsHasValidOperator = request.conditions.All(condition =>
        condition.@operator == ">" ||
        condition.@operator == "<" ||
        condition.@operator == "=" ||
        condition.@operator == "starts_with");

    //bad request any has invalid operator
    if (!allConditionsHasValidOperator)
        return Results.BadRequest("Invalid operator detected in conditions");



    //check db for policy name
    var dbPolicy = dbContext.Policies.FirstOrDefaultAsync(p => p.PolicyName == request.policyName);
    if (dbPolicy != null)
        return Results.BadRequest("Policy with same name already exists");

    //check distinct attributs definition exists
    var dbAttributes = new Dictionary<string, AttributeDefinition>();
    //get all attributes from DB with distinct for effiencey 
    foreach (var conditionAttributeName in request.conditions.Select(c => c.attributeName).Distinct()) 
    {
        var dbAttribtue = await dbContext.Attributes.FirstOrDefaultAsync(attribute => attribute.AttributeName == conditionAttributeName);

        if (dbAttribtue == null)
            return Results.BadRequest("Invalid attribute in condition, attribute is not defined");
        else
            dbAttributes[conditionAttributeName] = dbAttribtue;
    }

    PolicyDefinition policy = new PolicyDefinition() {  PolicyName = request.policyName };
    using (var t = await dbContext.Database.BeginTransactionAsync())
    {
        await dbContext.Policies.AddAsync(policy);
        await dbContext.PolicyConditions.AddRangeAsync(request.conditions.Select(c => new PolicyCondition
        {
            AttributeDefinitionId = dbAttributes[c.attributeName].AttributeDefinitionId,
            Operator = c.@operator,
            PolicyDefinitionId = policy.PolicyDefinitionId
        }));
        await dbContext.SaveChangesAsync();
        await t.CommitAsync();
    }

    return Results.Ok();
});



app.Run();

internal record AttributeDefinitionDto(string attributeName, string attributeType);
internal record PolicyDefinitionDto(string policyName, List<PolicyConditionDto> conditions);
internal record PolicyConditionDto(string attributeName, string @operator, string value);