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
        [Required(ErrorMessage = "Titlul este obligatoriu")]
        [MaxLength(100, ErrorMessage = "Titlul nu poate depăși 100 de caractere")]
        public string Title { get; set; }

        [MaxLength(1000, ErrorMessage = "Descrierea nu poate depăși 1000 de caractere")]
        public string? Description { get; set; }

        // Calea către imagine sau URL video
        public string? MediaUrl { get; set; }

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

        // Proprietate auxiliară pentru input-ul de tag-uri (opțional)
        [NotMapped]
        public string? TagString { get; set; }
        // 5. Comentarii
        public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();
    }
}