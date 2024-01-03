using LinxABAC.Database;
using LinxABAC.Models.AbacPermissions;
using LinxABAC.Queries;
using Microsoft.EntityFrameworkCore;

namespace LinxABAC.Logic
{
    public interface IPolicyService
    {
        public Task<string?> CreatePolicyAsync(PolicyDefinitionDto request);
    }
    public class PolicyService : IPolicyService
    {
        private readonly AppDbContext _dbContext;
        private readonly IRedisQueries _redisQueries;

        private static readonly IReadOnlyDictionary<string, IEnumerable<string>> AttributeTypesAllowdOperators = new Dictionary<string, IEnumerable<string>>()
        {
            { Constants.StringAttribute, new List<string>() { "=", "starts_with" } },
            { Constants.IntegerAttribute, new List<string>() { "<", ">", "=", } },
            { Constants.BooleanAttribute, new List<string>() { "=" } }
        };

        public PolicyService(AppDbContext dbContext, IRedisQueries redisQueries)
        {
            _dbContext = dbContext;
            _redisQueries = redisQueries;
        }

        /// <summary>
        /// creates a policy if validations are good, otherwise return an error message describing a problem
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<string?> CreatePolicyAsync(PolicyDefinitionDto request)
        {
            //check count of policies in the request
            if (request.conditions.Count >= Constants.MaxConditionsPerPolicy)
                return "Too many conditions for policy";

            if (request.conditions.Count == 0)
                return "Policy must have at least one condition";

            //check valid operator on all condiions
            bool allConditionsHasValidOperator = request.conditions.All(condition =>
                condition.@operator == ">" ||
                condition.@operator == "<" ||
                condition.@operator == "=" ||
                condition.@operator == "starts_with");

            //bad request any has invalid operator
            if (!allConditionsHasValidOperator)
                return "Invalid operator detected in conditions";

            //check db for policy name
            var dbPolicy = await _dbContext.Policies.FirstOrDefaultAsync(p => p.PolicyName == request.policyName);
            if (dbPolicy != null)
                return "Policy with same name already exists";

            //check distinct attributs definition exists
            var dbAttributes = new Dictionary<string, AttributeDefinition>();

            //get all attributes from DB with distinct for effiencey 
            foreach (var conditionAttributeName in request.conditions.Select(c => c.attributeName).Distinct())
            {
                var dbAttribtue = await _dbContext.Attributes.FirstOrDefaultAsync(attribute => attribute.AttributeName == conditionAttributeName);

                if (dbAttribtue == null)
                    return "Invalid attribute in condition, attribute is not defined";
                else
                    dbAttributes[conditionAttributeName] = dbAttribtue;
            }

            //check data types agains the operator with condition definition
            foreach (var condition in request.conditions)
            {
                var dbCondition = dbAttributes[condition.attributeName];
                bool operatorAllowedForType = AttributeTypesAllowdOperators[dbCondition.AttributeType].Contains(condition.@operator);
                if (!operatorAllowedForType)
                    return $"Invalid operator '{condition.@operator}' for type '{dbCondition.AttributeType}' in attribute '{condition.attributeName}'";
            }

            //save the data to DB
            PolicyDefinition policy = new PolicyDefinition() { PolicyName = request.policyName };

            await _dbContext.Policies.AddAsync(policy);
            await _dbContext.PolicyConditions.AddRangeAsync(request.conditions.Select(c => new PolicyCondition
            {
                AttributeDefinitionId = dbAttributes[c.attributeName].AttributeDefinitionId,
                PolicyDefinitionId = policy.PolicyDefinitionId,
                Operator = c.@operator,
                Value = c.value,
                Policy = policy
            }));
            await _dbContext.SaveChangesAsync();

            //Important!!
            //clear the evaluation of policy results for all users that it is precomputed
            _redisQueries.DeletePolicyUsersResults(policy.PolicyName);

            return null;
        }
    }
}
