using Microsoft.AspNetCore.Identity;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace dotNet_Project.Models
{
    public class ApplicationUser : IdentityUser
    {
        // Proprietăți extra pentru profil
        [MaxLength(30)]
        public string? FirstName { get; set; }
        [MaxLength(30)]
        public string? LastName { get; set; }
        public string? Description { get; set; } // Secțiunea About
        public string? ProfilePicture { get; set; }

        // Relații
        public virtual ICollection<Bookmark> Bookmarks { get; set; }
        public virtual ICollection<Category> Categories { get; set; }
    }
}
