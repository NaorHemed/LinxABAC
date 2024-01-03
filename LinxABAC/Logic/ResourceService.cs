using LinxABAC.Queries;
using System.ComponentModel.Design;

namespace LinxABAC.Logic
{
    public interface IResourceService
    {
        public bool CreateResource(CreateResourceRequest request);
    }
    public class ResourceService : IResourceService
    {
        private readonly IRedisQueries _redisQueries;
        private readonly ILogger<PolicyService> _logger;

        public ResourceService(IRedisQueries redisQueries, ILogger<PolicyService> logger)
        {
            _redisQueries = redisQueries;
            _logger = logger;
        }

        public bool CreateResource(CreateResourceRequest request)
        {
            if (_redisQueries.GetResourceCounter() >= Constants.MaxResources) 
            {
                _logger.LogWarning("Too many resources");
                return false;
            }

            if (request.Policies.Count == 0)
            {
                _logger.LogWarning($"Resource must have at leat one policy '{request.resourceName}'");
                return false;
            }

            if ( request.Policies.Count > Constants.MaxPoliciesPerResource) 
            {
                _logger.LogWarning($"Resource has too many policies policy '{request.resourceName}'");
                return false;
            }

            foreach (var policyName in request.Policies) 
            {
                if (!_redisQueries.PolicyExists(policyName))
                {
                    _logger.LogWarning($"Policy not found '{policyName}'");
                    return false;
                }
            }

            //save the resource to the database
            _redisQueries.SetResourcePolicies(request.resourceName, request.Policies);
            //increment counter
            _redisQueries.IncrementResourceCounter();

            return true;
        }
    }
}
