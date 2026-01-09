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

        public DateTime CreatedAt { get; set; }

        // Câmp pentru popularitate (numărul total de like-uri)
        public int LikeCount { get; set; } = 0;

        // Relația cu voturile
        public virtual ICollection<Vote> Votes { get; set; } = new List<Vote>();

        // --- Relații (Foreign Keys) ---

        // 1. Autorul Bookmark-ului
        [Required]
        public string UserId { get; set; }

        public virtual ApplicationUser? User { get; set; }

        // 2. Categoria
        [Required(ErrorMessage = "Selectarea unei categorii este obligatorie")]
        public int CategoryId { get; set; }

        public virtual Category? Category { get; set; }

        // --- Colecții ---

        // IMPORTANT: Aceasta este linia care lipsește și cauzează eroarea.
        // Trebuie să existe pentru ca EF Core și Controller-ul să funcționeze.
        public virtual ICollection<Tag> Tags { get; set; } = new List<Tag>();

        // NOTĂ: Am scos TagString (inputul manual), dar am păstrat Tags (relația din DB).
        // [NotMapped]
        // public string? TagString { get; set; }
    }
}