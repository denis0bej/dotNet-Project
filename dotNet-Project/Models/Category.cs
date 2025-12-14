using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace dotNet_Project.Models
{
    public class Category
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Category name is mandatory!")]
        public string Name { get; set; }

        [Display(Name = "Visibility")]
        public bool IsPublic { get; set; } = true;

        // Relația cu User-ul (opțional, dacă vrei categorii private)
        public string? UserId { get; set; }
        public virtual ApplicationUser? User { get; set; }

        public virtual ICollection<Bookmark> Bookmarks { get; set; } = [];
    }
}