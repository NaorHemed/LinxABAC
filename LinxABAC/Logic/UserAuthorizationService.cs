using LinxABAC.Queries;

namespace LinxABAC.Logic
{
    public interface IUserAuthorizationService
    {
        public bool IsAuthorized(string resourceName, string userId);
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

        public bool IsAuthorized(string resourceName, string userId)
        {
            var userLastUpdate = _redisQueries.GetUserLastUpdate(userId);
            //check that user and resource was not update after the recomputed result
            if (_redisQueries.GetUserResourceResultLastUpdate(userId, resourceName) > _redisQueries.GetResourceLastUpdate(resourceName) &&
                _redisQueries.GetUserResourceResultLastUpdate(userId, resourceName) > userLastUpdate)
            {
                //get the policy that was last used to authorize the user to the resource
                string? policyName = _redisQueries.GetResourceUserAccess(resourceName, userId);
                if (policyName != null)
                {
                    //check that user and policy not updated after precomputed result
                    if (_redisQueries.GetUserPolicyResultLastUpdate(userId, policyName) > _redisQueries.GetPolicyLastUpdate(policyName) &&
                        _redisQueries.GetUserPolicyResultLastUpdate(userId, policyName) > userLastUpdate)
                    {
                        return true;
                    }
                    else
                    {
                        //either the policy or user was updated, need to compute result again
                        return ComputeResourceResult(resourceName, userId);
                    }
                }
                else
                {
                    //there was no update since last time the user failed to neither the resource, the polocy or the user,
                    //if didnt pass last time it will not this time
                    return false;
                }
            }
            else
            {
                //either the resource or user was updated, need to compute result again
                return ComputeResourceResult(resourceName, userId);
            }
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
                    //setting result last update for user and resource
                    _redisQueries.SetUserResourceResultLastUpdate(userId, resourceName);
                    return true;
                }
            }
            //none of the policies match, therefore we can know it faster next time
            return false;
        }

        private bool ComputePolicyResult(string policyName, string userId)
        {
            //check previouslt computed policy result for user, if it was computed after change to user and policy, the result should not change
            bool? policyResult = _redisQueries.GetPolicyUsersResults(policyName, userId);
            if (policyResult.HasValue &&
                _redisQueries.GetUserPolicyResultLastUpdate(userId, policyName) > _redisQueries.GetPolicyLastUpdate(policyName) &&
                _redisQueries.GetUserPolicyResultLastUpdate(userId, policyName) > _redisQueries.GetUserLastUpdate(userId))
            {
                return policyResult.Value;
            }

            var policyConditions = _redisQueries.GetPolicy(policyName);
            var attributeValues = new Dictionary<string, string>();
            var attributeTypes = new Dictionary<string, string>();
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
            //set result last update for policy + user
            _redisQueries.SetUserPolicyResultLastUpdate(userId, policyName);
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
