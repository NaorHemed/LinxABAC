using LinxABAC.Queries;

namespace LinxABAC.Logic
{
    public interface IUserAuthorizationService
    {
        public bool IsAuthorized2(string resourceName, string userId);
    }
    public class UserAuthorizationService : IUserAuthorizationService
    {
        private readonly IRedisQueries _redisQueries;
        private readonly ILogger<UserAuthorizationService> _logger;

        public UserAuthorizationService(IRedisQueries redisQueries, ILogger<UserAuthorizationService> logger)
        {
            _redisQueries = redisQueries;
            _logger = logger;
        }

        public bool IsAuthorized2(string resourceName, string userId)
        {
            return ComputeResourceResult(resourceName, userId);
        }

        //todo
        public bool IsAuthorized(string userId, string resourceName)
        {
            //check if user was authorized to resource, and if so using which policy
            string? policyName = _redisQueries.GetResourceUserAccess(userId, resourceName);
            if (policyName != null)
            {
                //check that the policy that was used is stll valid now, and not cleard by any update to user or policy
                bool policyAllowed = _redisQueries.GetPolicyUsersResults(policyName, userId);
                if (policyAllowed)
                    return true;
            }
            return false;
        }

        private bool ComputeResourceResult(string resourceName, string userId)
        {
            var policies = _redisQueries.GetResourcePolicies(resourceName);
            //we will check each policy until the first passes
            //checking as little as policies as possible
            foreach (var policyName in policies) 
            {
                bool isAllowed = ComputePolicyResult(policyName, userId);
                if (isAllowed)
                { 
                    //setting successfull policy because we need one success to authorize
                    _redisQueries.SetResourceUserAccess(resourceName, userId, policyName);
                    return true;
                }
            }
            //none of the policies match, therefore we can know it faster next time
            return false;
        }

        private bool ComputePolicyResult(string policyName, string userId)
        {
            var policyConditions = _redisQueries.GetPolicy(policyName);
            var attributeValues = new Dictionary<string,string>();
            var attributeTypes = new Dictionary<string,string>();
            bool allowed = true;
            foreach (var policyCondition in policyConditions)
            {
                //evaluate the condition step by step instead of querying all user attributes
                //keep track on attribtues we already queried before
                //same for attributeValues and attributeTypes
                if (!attributeValues.TryGetValue(policyCondition.attributeName, out string? attributeValue))
                {
                    attributeValue = _redisQueries.GetUserAttribute(userId, policyCondition.attributeName);
                    attributeValues[policyCondition.attributeName] = attributeValue!;
                }

                if (!attributeTypes.TryGetValue(policyCondition.attributeName, out string? attributeType))
                {
                    attributeType = _redisQueries.GetAttributeDefinition(policyCondition.attributeName);
                    attributeTypes[policyCondition.attributeName] = attributeType!;
                }

                //if one of consition policy failed, policy has failed and not allowed
                if (EvaluateConditionOnAttribute(policyCondition, attributeType, attributeValue) == false)
                {
                    allowed = false;
                    break;
                } 
            }

            //save computed result
            _redisQueries.SetPolicyUsersResults(policyName, userId, allowed);

            return allowed;
        }

        private bool EvaluateConditionOnAttribute(PolicyConditionDto policyCondition, string? attributeType, string? attributeValue)
        {
            if (attributeType == null || attributeValue == null)
                return false;

            switch (policyCondition.@operator)
            {
                case ">" when attributeType == Constants.IntegerAttribute:
                    {
                        return int.Parse(attributeValue) > int.Parse(policyCondition.value);
                    }

                case "<" when attributeType == Constants.IntegerAttribute:
                    {
                        return int.Parse(attributeValue) < int.Parse(policyCondition.value);
                    }

                case "=" when attributeType == Constants.IntegerAttribute:
                    {
                        return int.Parse(attributeValue) == int.Parse(policyCondition.value);
                    }

                case "=" when attributeType == Constants.StringAttribute:
                    {
                        return attributeValue.Equals(policyCondition.value);
                    }

                case "=" when attributeType == Constants.BooleanAttribute:
                    {
                        return bool.Parse(attributeValue) == bool.Parse(policyCondition.value);
                    }

                case "starts_with" when attributeType == Constants.StringAttribute:
                    {
                        return attributeType.StartsWith(policyCondition.value);
                    }
                default:
                    {
                        _logger.LogWarning($"undifned condition logic operator='{policyCondition.@operator}' attributeType='{attributeType}' policyCondition.value='{policyCondition.value}'");
                        return false;
                    }
            }
        }
    }
}
