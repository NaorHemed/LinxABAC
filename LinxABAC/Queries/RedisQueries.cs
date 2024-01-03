using StackExchange.Redis;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LinxABAC.Queries
{
    public interface IRedisQueries
    {
        //user attribtues section
        public Dictionary<string, string>? GetUserAttributes(string userId);
        public string? GetUserAttribute(string userId, string attribute);
        public void SetUserAttribute(string userId, string attribute, string value);
        public void SetUserAttributes(string userId, Dictionary<string, string> attributes);
        public void DeleteUserAttribute(string userId, string attribute);
        //policy users results section
        public bool GetPolicyUsersResults(string policyName, string userId);
        public void SetPolicyUsersResults(string policyName, string userId, bool allowed);
        public void DeletePolicyUsersResults(string policyName);
        //resource computed results section
        public string? GetResourceUserAccess(string resourceName, string userId);
        public void SetResourceUserAccess(string resourceName, string userId, string policyName);
        //attribtues definitions
        public List<AttributeDefinitionDto> GetAttributeDefinitions();
        public bool SetAttributeDefinition(string attribute, string type);
        public string? GetAttributeDefinition(string attribute);
        //policy definitions
        public List<PolicyConditionDto> GetPolicy(string policyName);
        public void SetPolicy(string policyName, List<PolicyConditionDto> policyConditions);
        //resource definitions
        public List<string> GetResourcePolicies(string resourceName);
        public void SetResourcePolicies(string resourceName, List<string> resourcePolicies);
        //counters
        public long GetPolicyCounter();
        public void IncrementPolicyCounter();
        public long GetResourceCounter();
        public void IncrementResourceCounter();
        public long GetAttributesDefinitionCounter();
        public void IncrementAttributesDefinitionCounter();
        public long GetUsersCounter();
        public void IncrementUsersCounter();
    }
    public class RedisQueries : IRedisQueries
    {
        private readonly IConnectionMultiplexer _connectionMultiplexer;
        private readonly IDatabase _database;
        public RedisQueries(IConnectionMultiplexer connectionMultiplexer)
        {
            _connectionMultiplexer = connectionMultiplexer;
            _database = _connectionMultiplexer.GetDatabase();
        }

        public Dictionary<string,string>? GetUserAttributes(string userId)
        {
            return _database.HashGetAll($"UserAttributes_{userId}")
                .ToDictionary(h => h.Name.ToString(), h => h.Value.ToString());
        }

        public string? GetUserAttribute(string userId, string attribute)
        {
            //_database.sca

            return _database.HashGet($"UserAttributes_{userId}", attribute);
        }

        public void SetUserAttribute(string userId, string attribute, string value)
        {
            _database.HashSetAsync($"UserAttributes_{userId}", attribute, value);
        }

        public void SetUserAttributes(string userId, Dictionary<string,string> attributes) 
        {
            HashEntry[] hashEntries = attributes.Select(kv => new HashEntry(kv.Key, kv.Value)).ToArray();
            _database.HashSet($"UserAttributes_{userId}", hashEntries);
        }

        public void DeleteUserAttribute(string userId, string attribute)
        {
            _database.HashDelete($"UserAttributes_{userId}", attribute);
        }


        public bool GetPolicyUsersResults(string policyName, string userId)
        {
            var hash = _database.HashGet($"PolicyUsersResults_{policyName}", userId);
            return hash.HasValue && (bool)hash;
        }

        public void SetPolicyUsersResults(string policyName, string userId, bool allowed)
        {
            _database.HashSet($"PolicyUsersResults_{policyName}", userId.ToString(), allowed);
        }

        public void DeletePolicyUsersResults(string policyName)
        {
            _database.KeyDelete($"PolicyUsersResults_{policyName}");
        }

        /// <summary>
        /// returns the policy name that allowed the user access to the resource, null if not found
        /// </summary>
        /// <param name="resourceName"></param>
        /// <param name=""></param>
        /// <returns>the policy name if exists</returns>
        public string? GetResourceUserAccess(string resourceName, string userId)
        {
            return _database.HashGet($"ResourceUsersAccess_{resourceName}", userId.ToString());
        }

        public void SetResourceUserAccess(string resourceName, string userId, string policyName)
        {
            _database.HashSet($"ResourceUsersAccess_{resourceName}", userId.ToString(), policyName);
        }

        public List<AttributeDefinitionDto> GetAttributeDefinitions()
        {
            return _database.HashGetAll("AttributesDefinitions")
                .Select(kv => new AttributeDefinitionDto(kv.Name!, kv.Value!))
                .ToList();
        }

        public bool SetAttributeDefinition(string attribute, string type)
        {
            return _database.HashSet("AttributesDefinitions", attribute, type);
        }

        public string? GetAttributeDefinition(string attribute)
        {
            return _database.HashGet("AttributesDefinitions", attribute);
        }

        public List<PolicyConditionDto> GetPolicy(string policyName)
        {
            return _database.HashGetAll($"Policy_{policyName}")
                .Select((item, index) => JsonSerializer.Deserialize<PolicyConditionDto>(item.Value.ToString())!)
                .ToList();
        }

        public void SetPolicy(string policyName, List<PolicyConditionDto> policyConditions)
        {
            HashEntry[] hashEntries = policyConditions.Select((item, index) => new HashEntry(index, JsonSerializer.Serialize<PolicyConditionDto>(item))).ToArray();
            _database.HashSet($"Policy_{policyName}", hashEntries);
        }

        public List<string> GetResourcePolicies(string resourceName)
        {
            return _database.HashGetAll($"Resource_{resourceName}")
                .Select(kv => kv.Value.ToString())
                .ToList();
        }

        public void SetResourcePolicies(string resourceName, List<string> resourcePolicies)
        {
            HashEntry[] hashEntries = resourcePolicies.Select((item, index) => new HashEntry(index, item)).ToArray();
            _database.HashSet($"Resource_{resourceName}", hashEntries);
        }


        public long GetPolicyCounter()
        {
            _database.HashGet("Counters", "Policies").TryParse(out long count);
            return count;
        }

        public void IncrementPolicyCounter()
        {
            _database.HashIncrement("Counters", "Policies", 1);
        }

        public long GetResourceCounter()
        {
            _database.HashGet("Counters", "Resources").TryParse(out long count);
            return count;
        }

        public void IncrementResourceCounter()
        {
            _database.HashIncrement("Counters", "Resources", 1);
        }

        public long GetAttributesDefinitionCounter()
        {
            _database.HashGet("Counters", "Attributes").TryParse(out long count);
            return count;
        }

        public void IncrementAttributesDefinitionCounter()
        {
            _database.HashIncrement("Counters", "Attributes", 1);
        }

        public long GetUsersCounter()
        {
            _database.HashGet("Counters", "Users").TryParse(out long count);
            return count;
        }

        public void IncrementUsersCounter()
        {
            _database.HashIncrement("Counters", "Users", 1);
        }
    }
}
