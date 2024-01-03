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

            base.OnModelCreating(modelBuilder);
        }
    }
}
