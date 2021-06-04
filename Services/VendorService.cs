using System.Collections.Generic;
using System.Linq;
using newkilibraries;

namespace newki_inventory_vendor
{
    public interface IVendorService{
        List<Vendor> GetVendors();
        Vendor GetVendor(int id);
        void Insert(Vendor vendor);
        void Update(Vendor vendor);
        void Remove(int id);
    }
    public class VendorService : IVendorService
    {
       private readonly ApplicationDbContext _context;

        public VendorService(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<Vendor> GetVendors()
        {
            return _context.Vendor.OrderByDescending(p=>p.VendorId).ToList();
        }

        public Vendor GetVendor(int id)
        {  
            return _context.Vendor.FirstOrDefault(p=>p.VendorId == id);                
        } 

        public void Insert(Vendor vendor)
        {            
            _context.Vendor.Add(vendor);
            _context.SaveChanges();
        }
        public void Update(Vendor vendor)
        {            
            var existingVendor = _context.Vendor.Find(vendor.VendorId);
            _context.Entry(existingVendor).CurrentValues.SetValues(vendor);
            _context.SaveChanges();
        }
        public void Remove(int id)
        {            
            var vendor = _context.Vendor
                .Where(x => x.VendorId == id)
                .FirstOrDefault();
            _context.Vendor.Remove(vendor);
            _context.SaveChanges();
        }
    }
}