using LinxABAC.Queries;
using Microsoft.EntityFrameworkCore;

namespace LinxABAC.Logic
{
    public interface IPolicyService
    {
        public bool CreatePolicy(PolicyDefinitionDto request);
        public bool UpdatePolicy(string policyName, List<PolicyConditionDto> conditions);
    }
    public class PolicyService : IPolicyService
    {
        private readonly IRedisQueries _redisQueries;
        private readonly ILogger<PolicyService> _logger;

        private static readonly IReadOnlyDictionary<string, IEnumerable<string>> AttributeTypesAllowdOperators = new Dictionary<string, IEnumerable<string>>()
        {
            { Constants.StringAttribute, new List<string>() { "=", "starts_with" } },
            { Constants.IntegerAttribute, new List<string>() { "<", ">", "=", } },
            { Constants.BooleanAttribute, new List<string>() { "=" } }
        };

        public PolicyService(IRedisQueries redisQueries, ILogger<PolicyService> logger)
        {
            _redisQueries = redisQueries;
            _logger = logger;
        }

        /// <summary>
        /// creates a policy if validations are good, otherwise return an error message describing a problem
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public bool CreatePolicy(PolicyDefinitionDto request)
        {
            //check count of policies in the request
            if (request.conditions.Count >= Constants.MaxConditionsPerPolicy)
            {
                _logger.LogWarning("Too many conditions for policy");
                return false;
            }

            //check if policy is not empty
            if (request.conditions.Count == 0)
            {
                _logger.LogWarning("Policy must have at least one condition");
                return false;
            }

            //check total policy counter in syste,
            if (_redisQueries.GetPolicyCounter() >= Constants.MaxPolicies)
            {
                _logger.LogWarning("Too many policies");
                return false;
            }

            //check db for policy name, when policy not exist empty list is returned
            var dbPolicy = _redisQueries.GetPolicy(request.policyName);
            if (dbPolicy != null && dbPolicy.Count > 0)
            {
                _logger.LogWarning($"Policy with same name already exists '{request.policyName}'");
                return false; 
            }

            if (!validatePolicyCondition(request.conditions))
            {
                return false;
            }

            //save the data
            SavePolicy(request.policyName, request.conditions);

            return true;
        }

        private bool validatePolicyCondition(List<PolicyConditionDto> conditions)
        {
            //check data types agains the operator with condition definition
            foreach (var condition in conditions)
            {
                //check if attribute defined
                var attributeType = _redisQueries.GetAttributeDefinition(condition.attributeName);
                if (attributeType == null)
                {
                    _logger.LogWarning($"Policy condition attribute doesnt exists '{condition.attributeName}'");
                    return false;
                }

                //check if operator is valid for attribute type
                bool operatorAllowedForType = AttributeTypesAllowdOperators[attributeType].Contains(condition.@operator);
                if (!operatorAllowedForType)
                {
                    _logger.LogWarning($"Invalid operator '{condition.@operator}' for type '{attributeType}' in attribute '{condition.attributeName}'");
                    return false;
                }
            }
            return true;
        }

        public bool UpdatePolicy(string policyName, List<PolicyConditionDto> conditions)
        {
            //get policy from db and check it is valid
            var dbPolicy = _redisQueries.GetPolicy(policyName);
            if (dbPolicy == null || dbPolicy.Count == 0)
            {
                _logger.LogWarning($"Policy doest not exists '{policyName}'");
                return false;
            }

            if (!validatePolicyCondition(conditions))
                return false;

            //save the data
            SavePolicy(policyName, conditions);

            return true;
        }

        private void SavePolicy(string policyName, List<PolicyConditionDto> conditions)
        {
            //save the new policy
            _redisQueries.SetPolicy(policyName, conditions);

            //clear precomputed user policy results because its new policy condition
            _redisQueries.DeletePolicyUsersResults(policyName);

            //set last update time
            _redisQueries.SetPolicyLastUpdate(policyName);
        }
    }
}
