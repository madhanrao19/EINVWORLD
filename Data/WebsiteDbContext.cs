using Microsoft.EntityFrameworkCore;
using EINVWORLD.Models.Public;

namespace EINVWORLD.Data
{
    public class WebsiteDbContext : DbContext
    {
        public WebsiteDbContext(DbContextOptions<WebsiteDbContext> options) : base(options) { }

        public DbSet<ResourceItem> Resources { get; set; } = default!;
        public DbSet<ResourceType> ResourceTypes { get; set; } = default!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure string-based FK
            modelBuilder.Entity<ResourceType>()
                .HasKey(rt => rt.Code);

            //modelBuilder.Entity<ResourceItem>()
            //    .HasOne(ri => ri.ResourceType)
            //    .WithMany()
            //    .HasForeignKey(ri => ri.ResourceTypeCode)
            //    .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
