using LinxABAC.Models.AbacPermissions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LinxABAC.Database
{
    public class AppDbContext : DbContext
    {
        public DbSet<AttributeDefinition> Attributes { get; set; }
        public DbSet<PolicyDefinition> Policies { get; set; }
        public DbSet<PolicyCondition> PolicyConditions { get; set; }
        public DbSet<ResourceDefinition> Resources { get; set; }

        public AppDbContext(DbContextOptions options) : base(options)
        {
                
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AttributeDefinition>()
                .HasIndex(entity => entity.AttributeName).IsUnique(); // no duplicate attribute names

            modelBuilder.Entity<PolicyDefinition>()
                .HasIndex(entity => entity.PolicyName).IsUnique(); // no duplicate policy names

            modelBuilder.Entity<ResourceDefinition>()
                .HasIndex(entity => entity.ResourceName).IsUnique(); // no duplicate resource names

            modelBuilder.Entity<PolicyCondition>(entity =>
            {
                //many to one between policy condition and policy
                entity.HasOne(e => e.Policy).WithMany(e => e.PolicyConditions)
                   .HasForeignKey(e => e.PolicyDefinitionId)
                   .OnDelete(DeleteBehavior.Cascade); //when delete policy removes all its conditions

                //many to one between policy condition and attribute
                entity.HasOne(e => e.Attribute).WithMany(e => e.PolicyConditions)
                    .HasForeignKey(e => e.AttributeDefinitionId)
                    .OnDelete(DeleteBehavior.Restrict); //prevent delete attribute which is used by condition
            });

            //many to many table between resource and policy
            modelBuilder.Entity<ResourcePoliciesDefinition>(entity =>
            {
                entity.HasKey(e => new { e.PolicyDefinitionId, e.ResourceDefinitionId }); //primary key

                entity.HasOne(e => e.Policy).WithMany(e => e.ResourcePolicies)
                    .HasForeignKey(e => e.PolicyDefinitionId)
                    .OnDelete(DeleteBehavior.Restrict); //prevent delete policy used by resource


                entity.HasOne(e => e.Resource).WithMany(e => e.ResourcePolicies)
                    .HasForeignKey(e => e.ResourceDefinitionId)
                    .OnDelete(DeleteBehavior.Restrict); //prevent delete resource used by policy
            });


            base.OnModelCreating(modelBuilder);
        }
    }
}
