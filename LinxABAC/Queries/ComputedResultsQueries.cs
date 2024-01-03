using StackExchange.Redis;

namespace LinxABAC.Queries
{
    public interface IComputedResultsQueries
    {
        //user attribtues section
        public List<string>? GetUserAttributes(int userId);
        public string? GetUserAttribute(int userId, string attribute);
        public void SetUserAttribute(int userId, string attribute, string value);
        public void DeleteUserAttribute(int userId, string attribute);
        //policy users results section
        public bool GetPolicyUsersResults(string policyName, int userId);
        public void SetPolicyUsersResults(string policyName, int userId, bool allowed);
        public void DeletePolicyUsersResults(string policyName);
        //resource computed results section
        public string? GetResourceUserAccess(string resourceName, int userId);
        public void SetResourceUserAccess(string resourceName, int userId, string policyName);
    }
    public class ComputedResultsQueries : IComputedResultsQueries
    {
        private readonly IConnectionMultiplexer _connectionMultiplexer;
        private readonly IDatabase _database;
        public ComputedResultsQueries(IConnectionMultiplexer connectionMultiplexer)
        {
            _connectionMultiplexer = connectionMultiplexer;
            _database = _connectionMultiplexer.GetDatabase();
        }

        public List<string>? GetUserAttributes(int userId)
        {
            return _database.HashGetAll($"UserAttributes_{userId}").Select(h => h.ToString()).ToList();
        }


        public string? GetUserAttribute(int userId, string attribute)
        {
            return _database.HashGet($"UserAttributes_{userId}", attribute);
        }

        public void SetUserAttribute(int userId, string attribute, string value)
        {
            _database.HashSetAsync($"UserAttributes_{userId}", attribute, value);
        }

        public void DeleteUserAttribute(int userId, string attribute)
        {
            _database.HashDelete($"UserAttributes_{userId}", attribute);
        }


        public bool GetPolicyUsersResults(string policyName, int userId)
        {
            var hash = _database.HashGet($"PolicyUsersResults_{policyName}", userId);
            return hash.HasValue && (bool)hash;
        }

        public void SetPolicyUsersResults(string policyName, int userId, bool allowed)
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
        public string? GetResourceUserAccess(string resourceName, int userId)
        {
            return _database.HashGet($"ResourceUsersAccess_{resourceName}", userId.ToString());
        }

        public void SetResourceUserAccess(string resourceName, int userId, string policyName)
        {
            _database.HashSet($"ResourceUsersAccess_{resourceName}", userId.ToString(), policyName);
        }
    }
}
