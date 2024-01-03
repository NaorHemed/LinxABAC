using StackExchange.Redis;

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
    }
}
