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
        public DateTime CreatedAt { get; set; }

        // --- Relații (Foreign Keys) ---

        // 1. Autorul Bookmark-ului
        [Required]
        public string UserId { get; set; }

        // NOTĂ: DE FACUT ApplicationUser

        public virtual ApplicationUser User { get; set; }

        // 2. Categoria
        [Required(ErrorMessage = "Selectarea unei categorii este obligatorie")]
        public int CategoryId { get; set; }

        // NOTĂ: DE FACUT Category

        public virtual Category Category { get; set; }

        // --- Colecții ---

        // NOTĂ: DE FACUT Comment, Like, Tag
        //public virtual ICollection<Comment> Comments { get; set; }
        //public virtual ICollection<Like> Likes { get; set; }
        //public virtual ICollection<Tag> Tags { get; set; }
    }
}