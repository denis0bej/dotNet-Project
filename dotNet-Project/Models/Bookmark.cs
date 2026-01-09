using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Xml.Linq;

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

        // Data creării (necesară pentru filtrarea "Cele mai recente")

        public Boolean IsPublic { get; set; } = true;
        public DateTime CreatedAt { get; set; }

        // --- Relații (Foreign Keys) ---

        // 1. Autorul Bookmark-ului
        [Required]
        public string UserId { get; set; }

        // NOTĂ: DE FACUT ApplicationUser

        public virtual ApplicationUser? User { get; set; }

        // 2. Categoria
        //public int CategoryId { get; set; }
        //public virtual Category? Category { get; set; }

        public virtual ICollection<Category> Categories { get; set; } = new List<Category>();

        // --- Colecții ---

        // Relația Many-to-Many: Un bookmark are mai multe tag-uri
        public virtual ICollection<Tag> Tags { get; set; } = new List<Tag>();

        [NotMapped]
        public string? TagString { get; set; }

        // NOTĂ: DE FACUT Comment, Like
        //public virtual ICollection<Comment> Comments { get; set; }
        //public virtual ICollection<Like> Likes { get; set; }
    }
}