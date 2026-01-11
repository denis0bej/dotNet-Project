using System;
using System.ComponentModel.DataAnnotations;

namespace dotNet_Project.Models
{
    public class Comment
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Comment cannot be empty.")]
        [StringLength(400, MinimumLength = 5, ErrorMessage = "Comment need to be between {2} and {1} characters long.")]
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