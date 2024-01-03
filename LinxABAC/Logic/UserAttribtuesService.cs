
using LinxABAC.Queries;
using Microsoft.EntityFrameworkCore;

namespace LinxABAC.Logic
{
    public interface IUserAttribtuesService
    {
        public Guid? CreateUser(Dictionary<string, string> attributes);
        public bool SetUserAttributes(Guid userId, Dictionary<string, string> attributes);
    }
    public class UserAttribtuesService : IUserAttribtuesService
    {
        private readonly IRedisQueries _redisQueries;
        private readonly ILogger<UserAttribtuesService> _logger;
        public UserAttribtuesService(IRedisQueries redisQueries, ILogger<UserAttribtuesService> logger)
        {
            _redisQueries = redisQueries;
            _logger = logger;
        }

        /// <summary>
        /// gets userId and attributes, validate attribtues data type and set the values
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="attributes"></param>
        /// <returns>null for sucess, error string for validation failure</returns>
        public bool ValidateAttributeTypes(Dictionary<string, string> attributes)
        {
            foreach (var attributeKV in attributes)
            {
                string? attributeType = _redisQueries.GetAttributeDefinition(attributeKV.Key);

                if (attributeType == null)
                {
                    _logger.LogWarning($"Failed to find attribute definition for '{attributeKV.Key}'");
                    return false;
                }

                if (attributeType == Constants.IntegerAttribute && !int.TryParse(attributeKV.Value, out _))
                {
                    _logger.LogWarning($"Invalid attribute type for '{attributeKV.Key}'");
                    return false;
                }

                if (attributeType == Constants.BooleanAttribute && !bool.TryParse(attributeKV.Value, out _))
                {
                    _logger.LogWarning($"Invalid attribute type for '{attributeKV.Key}'");
                    return false; 
                }
            }
            return true; //success
        }

        public Guid? CreateUser(Dictionary<string, string> attributes)
        {
            //check user capacity if its new user
            if (_redisQueries.GetUsersCounter() >= Constants.MaxUsers)
            {
                _logger.LogWarning("Too many users");
                return null;
            }

            Guid userId = Guid.NewGuid();

            if (SetUserAttributes(userId, attributes))
            {
                _redisQueries.IncrementUsersCounter();
                return userId;
            }
            else
            {
                _logger.LogWarning($"Failed to create user '{userId}'");
                return null;
            }
        }

        public bool SetUserAttributes(Guid userId, Dictionary<string, string> attributes)
        {
            //check valid data types to attribute definition
            bool validDataTypes = ValidateAttributeTypes(attributes);

            if (validDataTypes == false)
                return false;

            _redisQueries.SetUserAttributes(userId.ToString(), attributes);

            return true; //success
        }
    }
}
