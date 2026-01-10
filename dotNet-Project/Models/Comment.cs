using System;
using System.ComponentModel.DataAnnotations;

namespace dotNet_Project.Models
{
    public class Comment
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Comentariul nu poate fi gol.")]
        [MaxLength(500, ErrorMessage = "Comentariul nu poate depăși 500 de caractere.")]
        public string Content { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // --- Relații ---

        // Bookmark-ul la care se referă comentariul
        public int BookmarkId { get; set; }
        public virtual Bookmark Bookmark { get; set; }

        // Utilizatorul care a postat comentariul
        public string UserId { get; set; }
        public virtual ApplicationUser User { get; set; }
    }
}