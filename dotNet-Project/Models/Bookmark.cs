using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace dotNet_Project.Models
{
    public class Bookmark
    {
        [Key]
        public int Id { get; set; }

        // --- Date Generale ---
        [Required(ErrorMessage = "Title is mandatory")]
        [StringLength(100, MinimumLength = 5, ErrorMessage = "Title needs to be between {2} and {1} long.")]
        public string Title { get; set; }

        [MaxLength(1000, ErrorMessage = "Description cannot be longer than 1000 characters.")]
        public string? Description { get; set; }

        // Calea către imagine sau URL video
        public string? MediaUrl { get; set; } 

        [NotMapped]
        [Display(Name = "Upload Image")]
        public IFormFile? ImageUpload { get; set; }

        [Display(Name = "Public")]
        public bool IsPublic { get; set; } = true;

        public DateTime CreatedAt { get; set; }

        // --- Relații (Foreign Keys) ---

        // 1. Autorul Bookmark-ului
        [Required]
        public string UserId { get; set; }
        public virtual ApplicationUser? User { get; set; }

        // 2. Categoriile (Many-to-Many)
        // ATENȚIE: Aici era problema. Am scos CategoryId și am pus lista Categories.
        public virtual ICollection<Category> Categories { get; set; } = new List<Category>();

        // 3. Tag-uri (Many-to-Many)
        public virtual ICollection<Tag> Tags { get; set; } = new List<Tag>();

        // 4. Voturi/Likes
        public virtual ICollection<Vote> Votes { get; set; } = new List<Vote>();

        // Proprietate calculată pentru a număra like-urile rapid
        public int LikeCount { get; set; } = 0;

        
        
        
        [NotMapped]
        public string? TagString { get; set; }
        // 5. Comentarii
        public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();
    }
}