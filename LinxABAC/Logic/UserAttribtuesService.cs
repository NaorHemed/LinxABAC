using LinxABAC.Database;
using LinxABAC.Queries;
using Microsoft.EntityFrameworkCore;

namespace LinxABAC.Logic
{
    public interface IUserAttribtuesService
    {
        public Task<string?> ValidateAttributeTypesAsync(Dictionary<string, string> attributes);
        public Task<string?> SetUserAttributesAsync(Guid userId, Dictionary<string, string> attributes);
    }
    public class UserAttribtuesService : IUserAttribtuesService
    {
        private readonly AppDbContext _dbContext;
        private readonly IRedisQueries _redisQueries;
        public UserAttribtuesService(AppDbContext dbContext, IRedisQueries redisQueries)
        {
            _dbContext = dbContext;
            _redisQueries = redisQueries;
        }

        /// <summary>
        /// gets userId and attributes, validate attribtues data type and set the values
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="attributes"></param>
        /// <returns>null for sucess, error string for validation failure</returns>
        public async Task<string?> ValidateAttributeTypesAsync(Dictionary<string,string> attributes)
        {
            foreach (var attributeKV in attributes)
            {
                var attribute = await _dbContext.Attributes.FirstOrDefaultAsync(a => a.AttributeName == attributeKV.Key);

                if (attribute == null)
                    return $"Failed to find attribute definition for '{attributeKV.Key}'";

                if (attribute.AttributeType == Constants.IntegerAttribute && !int.TryParse(attributeKV.Value, out _))
                    return $"Invalid attribute type for '{attributeKV.Key}'";

                if (attribute.AttributeType == Constants.BooleanAttribute && !bool.TryParse(attributeKV.Value, out _))
                    return $"Invalid attribute type for '{attributeKV.Key}'";
            }
            return null; //success, no error
        }

        public async Task<string?> SetUserAttributesAsync(Guid userId, Dictionary<string, string> attributes)
        {
            string? errorMsg = await ValidateAttributeTypesAsync(attributes);

            //validation error, dont continue
            if (errorMsg != null) 
                return errorMsg;

            _redisQueries.SetUserAttributes(userId.ToString(), attributes);
            return null; //success, no error
        }
    }
}
