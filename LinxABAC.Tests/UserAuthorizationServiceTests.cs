using LinxABAC.Logic;
using LinxABAC.Queries;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;

namespace LinxABAC.Tests
{
    public class UserAuthorizationServiceTests
    {
        private readonly UserAuthorizationService sut;
        Mock<IRedisQueries> redisQueriesMock = new Mock<IRedisQueries>();
        Mock<ILogger<UserAuthorizationService>> loggerMock = new Mock<ILogger<UserAuthorizationService>>();

        private readonly string userId = Guid.NewGuid().ToString();
        public UserAuthorizationServiceTests()
        {
            sut = new UserAuthorizationService(redisQueriesMock.Object, loggerMock.Object);
        }


        [Fact]
        public void WhenAllConditionsOfAllTypeIsTrue_PolicyAllowed()
        {
            //arrange
            string resource = "resource";
            string policy = "policy";
            redisQueriesMock.Setup(x => x.GetResourcePolicies(It.IsAny<string>()))
                .Returns(new List<string>() { policy });

            redisQueriesMock.Setup(x => x.GetPolicy(policy))
                .Returns(new List<PolicyConditionDto>
                {
                    { new PolicyConditionDto("x", ">", "3") },
                    { new PolicyConditionDto("x", "<", "7") },
                    { new PolicyConditionDto("x", "=", "5") },
                    { new PolicyConditionDto("b", "=", "false") },
                    { new PolicyConditionDto("str", "=", "hello world") },
                    { new PolicyConditionDto("str", "starts_with", "hello") },
                });

            Dictionary<string, string> userAttributes = new Dictionary<string, string>()
            {
                { "x", "5" }, { "b" , "false" }, { "str", "hello world" }
            };

            Dictionary<string, string> attributeDefnitions = new Dictionary<string, string>()
            {
                { "x",Constants.IntegerAttribute }, { "b", Constants.BooleanAttribute }, { "str", Constants.StringAttribute },
            };

            redisQueriesMock.Setup(x => x.GetUserAttributes(userId)).Returns(userAttributes);

            redisQueriesMock.Setup(x => x.GetUserAttribute(userId, It.IsAny<string>()))
                .Returns<string, string>((_, attribute) => userAttributes[attribute]);

            redisQueriesMock.Setup(x => x.GetAttributeDefinition(It.IsAny<string>()))
                .Returns<string>((attribute) => attributeDefnitions[attribute]);

            redisQueriesMock.Setup(x => x.GetUserLastUpdate(userId)).Returns(0);
            redisQueriesMock.Setup(x => x.GetPolicyLastUpdate(policy)).Returns(0);
            redisQueriesMock.Setup(x => x.GetResourceLastUpdate(resource)).Returns(0);
            redisQueriesMock.Setup(x => x.GetUserResourceResultLastUpdate(userId, resource)).Returns(0);
            redisQueriesMock.Setup(x => x.GetUserPolicyResultLastUpdate(userId, policy)).Returns(0);

            //act 
            bool result = sut.IsAuthorized(resource, userId);

            //assert
            Assert.True(result);

            //verify caching was set for policy result
            redisQueriesMock.Verify(x => x.SetPolicyUsersResults(policy, userId, true), Times.Once());
            //verify we set this policy authorized this resource only when success
            redisQueriesMock.Verify(x => x.SetResourceUserAccess(resource, userId, policy) ,Times.Once());
            //verify we set last update time on the computed results
            redisQueriesMock.Verify(x => x.SetUserPolicyResultLastUpdate(userId, policy), Times.Once());   
            redisQueriesMock.Verify(x => x.SetUserResourceResultLastUpdate(userId, resource), Times.Once());
        }

        [Fact]
        public void When1PolicyFaileAnd1PolicyAllowed_ResourceIsAllowed()
        {
            //arrange
            string resource = "resource";
            string policy1 = "policy1"; //need to fail
            string policy2 = "policy2"; //need to pass
            redisQueriesMock.Setup(x => x.GetResourcePolicies(It.IsAny<string>()))
                .Returns(new List<string>() { policy1, policy2 });

            redisQueriesMock.Setup(x => x.GetPolicy(policy1))
                .Returns(new List<PolicyConditionDto>
                {
                    { new PolicyConditionDto("x", ">", "3") },
                    { new PolicyConditionDto("x", "<", "7") },
                });

            redisQueriesMock.Setup(x => x.GetPolicy(policy2))
                .Returns(new List<PolicyConditionDto>
                {
                    { new PolicyConditionDto("str", "starts_with", "hello") },
                });

            Dictionary<string, string> userAttributes = new Dictionary<string, string>()
            {
                { "x", "9" }, // 3 < x < 7 => false
                { "str", "hello world" } // starts with 'hello' => true
            };

            Dictionary<string, string> attributeDefnitions = new Dictionary<string, string>()
            {
                { "x",Constants.IntegerAttribute }, { "str", Constants.StringAttribute },
            };

            redisQueriesMock.Setup(x => x.GetUserAttributes(userId)).Returns(userAttributes);

            redisQueriesMock.Setup(x => x.GetUserAttribute(userId, It.IsAny<string>()))
                .Returns<string, string>((_, attribute) => userAttributes[attribute]);

            redisQueriesMock.Setup(x => x.GetAttributeDefinition(It.IsAny<string>()))
                .Returns<string>((attribute) => attributeDefnitions[attribute]);

            redisQueriesMock.Setup(x => x.GetUserLastUpdate(userId)).Returns(0);
            redisQueriesMock.Setup(x => x.GetPolicyLastUpdate(It.IsAny<string>())).Returns(0);
            redisQueriesMock.Setup(x => x.GetResourceLastUpdate(resource)).Returns(0);
            redisQueriesMock.Setup(x => x.GetUserResourceResultLastUpdate(userId, resource)).Returns(0);
            redisQueriesMock.Setup(x => x.GetUserPolicyResultLastUpdate(userId, It.IsAny<string>())).Returns(0);

            //act 
            bool result = sut.IsAuthorized(resource, userId);

            //assert
            Assert.True(result);

            //verify caching was set for policy result
            redisQueriesMock.Verify(x => x.SetPolicyUsersResults(policy1, userId, false), Times.Once());
            redisQueriesMock.Verify(x => x.SetPolicyUsersResults(policy2, userId, true), Times.Once());
            //verify we set this policy authorized this resource only when success
            redisQueriesMock.Verify(x => x.SetResourceUserAccess(resource, userId, policy1), Times.Never());
            redisQueriesMock.Verify(x => x.SetResourceUserAccess(resource, userId, policy2), Times.Once());
            //verify we set last update time on the computed results
            redisQueriesMock.Verify(x => x.SetUserPolicyResultLastUpdate(userId, policy1), Times.Once());
            redisQueriesMock.Verify(x => x.SetUserPolicyResultLastUpdate(userId, policy2), Times.Once());
            redisQueriesMock.Verify(x => x.SetUserResourceResultLastUpdate(userId, resource), Times.Once());
        }

        [Fact]
        public void WhenOnePolicyConditionFail_PolicyFail()
        {
            //arrange
            string resource = "resource";
            string policy = "policy";
            redisQueriesMock.Setup(x => x.GetResourcePolicies(It.IsAny<string>()))
                .Returns(new List<string>() { policy });

            redisQueriesMock.Setup(x => x.GetPolicy(policy))
                .Returns(new List<PolicyConditionDto>
                {
                    //cannot be 5,6 at same time
                    { new PolicyConditionDto("x", "=", "5") }, 
                    { new PolicyConditionDto("x", "=", "6") },
                });

            Dictionary<string, string> userAttributes = new Dictionary<string, string>()
            {
                { "x", "5" }, 
            };

            Dictionary<string, string> attributeDefnitions = new Dictionary<string, string>()
            {
                { "x",Constants.IntegerAttribute }
            };

            redisQueriesMock.Setup(x => x.GetUserAttributes(userId)).Returns(userAttributes);

            redisQueriesMock.Setup(x => x.GetUserAttribute(userId, It.IsAny<string>()))
                .Returns<string, string>((_, attribute) => userAttributes[attribute]);

            redisQueriesMock.Setup(x => x.GetAttributeDefinition(It.IsAny<string>()))
                .Returns<string>((attribute) => attributeDefnitions[attribute]);

            redisQueriesMock.Setup(x => x.GetUserLastUpdate(userId)).Returns(0);
            redisQueriesMock.Setup(x => x.GetPolicyLastUpdate(policy)).Returns(0);
            redisQueriesMock.Setup(x => x.GetResourceLastUpdate(resource)).Returns(0);
            redisQueriesMock.Setup(x => x.GetUserResourceResultLastUpdate(userId, resource)).Returns(0);
            redisQueriesMock.Setup(x => x.GetUserPolicyResultLastUpdate(userId, policy)).Returns(0);

            //act 
            bool result = sut.IsAuthorized(resource, userId);

            //assert
            Assert.False(result);

            //verify caching was set for policy result
            redisQueriesMock.Verify(x => x.SetPolicyUsersResults(policy, userId, false), Times.Once());
            //verify we set this policy authorized this resource only when success
            redisQueriesMock.Verify(x => x.SetResourceUserAccess(resource, userId, policy), Times.Never());
            //verify we set last update time on the computed results
            redisQueriesMock.Verify(x => x.SetUserPolicyResultLastUpdate(userId, policy), Times.Once());
            //on resource we set success only, so should not be called
            redisQueriesMock.Verify(x => x.SetUserResourceResultLastUpdate(userId, resource), Times.Never());
        }
    }
}