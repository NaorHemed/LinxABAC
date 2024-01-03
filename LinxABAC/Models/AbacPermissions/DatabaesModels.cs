using System.ComponentModel.DataAnnotations;

namespace LinxABAC.Models.AbacPermissions
{
    public class AttributeDefinition
    {
        [Key]
        public int AttributeDefinitionId { get; set; }
        [Required, MaxLength(128)]
        public string AttributeName { get; set; } = null!;
        [Required, MaxLength(16)]
        public string AttributeType { get; set; } = null!;
    }

    public class PolicyDefinition
    {
        [Key]
        public int PolicyDefinitionId { get; set; }
        [Required, MaxLength(128)]
        public string PolicyName { get; set; } = null!;
    }

    public class PolicyCondition
    {
        [Key]
        public int PolicyConditionId { get; set; }
        [Required, MaxLength(16)]
        public string Operator { get; set; } = null!;
        [Required, MaxLength(128)]
        public string Value { get;set; } = null!;

        public int PolicyDefinitionId { get; set; }
        public PolicyDefinition Policy { get; set; } = null!; //navigation proprty for entity framework
        public int AttributeDefinitionId { get; set; }    
        public AttributeDefinition Attribute { get; set; } = null!; //navigation proprty for entity framework
    }

    public class ResourceDefinition
    {
        [Key]
        public int ResourceDefinitionId { get; set; }

        [Required, MaxLength(128)]
        public string ResourceName { get; set; }
    }
}
