using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using dotNet_Project.Models;

namespace dotNet_Project.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Bookmark> Bookmarks { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Tag> Tags { get; set; }
        public DbSet<Vote> Votes { get; set; }
        public DbSet<Comment> Comments { get; set; }

        // --- ADAUGĂ ACEASTĂ METODĂ ---
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Rezolvăm eroarea "Multiple cascade paths"
            // Spunem bazei de date: "Dacă ștergem un User, NU șterge automat Voturile prin această relație directă"
            // (Ele vor fi șterse oricum logic sau prin relația cu Bookmark, sau manual)
            builder.Entity<Vote>()
                .HasOne(v => v.User)
                .WithMany()
                .HasForeignKey(v => v.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            // Configurare pentru a evita eroarea "Multiple Cascade Paths"
            builder.Entity<Comment>()
                .HasOne(c => c.User)
                .WithMany() // Nu avem neapărat o listă de Comments în User, e ok așa
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Restrict); // AICI este fix-ul: schimbăm din Cascade în Restrict
        }
    }
}