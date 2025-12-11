using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace dotNet_Project.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }
        // Aici vei adauga mai tarziu:
        // public DbSet<Bookmark> Bookmarks { get; set; }
        // Dar momentan merge si fara, doar pentru configurare.
    }
}
