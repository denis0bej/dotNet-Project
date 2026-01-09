using System.ComponentModel.DataAnnotations;

namespace dotNet_Project.Models
{
    public class Vote
    {
        [Key]
        public int Id { get; set; }

        // Cine a votat
        [Required]
        public string UserId { get; set; }
        public virtual ApplicationUser User { get; set; }

        // Ce a votat
        [Required]
        public int BookmarkId { get; set; }
        public virtual Bookmark Bookmark { get; set; }
    }
}