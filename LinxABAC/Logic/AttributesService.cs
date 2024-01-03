using LinxABAC.Queries;
using StackExchange.Redis;

namespace LinxABAC.Logic
{
    public interface IAttributesService
    {
        public bool CreateAttribute(string attributeName, string attributeType);
    }
    public class AttributesService : IAttributesService
    {
        private readonly IRedisQueries _redisQueries;
        private readonly ILogger<AttributesService> _logger;
        public AttributesService(IRedisQueries redisQueries, ILogger<AttributesService> logger)
        {
            _redisQueries = redisQueries;
            _logger = logger;
        }

        public bool CreateAttribute(string attributeName, string attributeType)
        {
            //check valid attribute type
            if (attributeType != Constants.IntegerAttribute &&
                attributeType != Constants.StringAttribute &&
                attributeType != Constants.BooleanAttribute)
            {
                _logger.LogWarning($"Invalid attribute type '{attributeType}'");
                return false;
            }

            //check attribute system capacity
            if (_redisQueries.GetAttributesDefinitionCounter() >= Constants.MaxAttributes)
            {
                _logger.LogWarning("Too many attributes");
                return false;
            }

            //check if already exists;
            var x = _redisQueries.GetAttributeDefinition(attributeName);
            if (_redisQueries.GetAttributeDefinition(attributeName) != null)
            {
                _logger.LogWarning($"Attribute already exists in the system '{attributeName}'");
                return false;
            }

            //create attribute in database
            _redisQueries.SetAttributeDefinition(attributeName, attributeType);

            //increment toatl attributes counter
            _redisQueries.IncrementAttributesDefinitionCounter();
            return true;
        }
    }
}
