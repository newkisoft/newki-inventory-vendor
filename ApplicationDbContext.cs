using Microsoft.EntityFrameworkCore;
using newkilibraries;

namespace newki_inventory_vendor
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.Entity<Vendor>().HasKey(sc => new { sc.VendorId});            
            builder.Entity<VendorDataView>().HasKey(sc => new { sc.VendorId});            
        }
        public DbSet<Vendor> Vendor { get; set; }
        public DbSet<VendorDataView> VendorDataView { get; set; }
    }
}