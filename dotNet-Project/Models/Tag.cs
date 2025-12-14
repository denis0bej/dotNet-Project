using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace dotNet_Project.Models
{
    public class Tag
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Numele tag-ului este obligatoriu")]
        [MaxLength(50, ErrorMessage = "Numele tag-ului nu poate depăși 50 de caractere")]
        public string Name { get; set; }

        // Relația Many-to-Many: Un tag apare în mai multe bookmark-uri
        public virtual ICollection<Bookmark> Bookmarks { get; set; } = new List<Bookmark>();
    }
}