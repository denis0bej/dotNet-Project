using System.Collections.Generic;

namespace dotNet_Project.Models
{
    public class BookmarkCardViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string? MediaUrl { get; set; } // Poate fi null
        public int LikeCount { get; set; }
        public bool IsLikedByCurrentUser { get; set; }
        public List<string> CategoryNames { get; set; } = new List<string>();

        public List<string> Tags { get; set; } = new List<string>();
        public bool IsPublic { get; set; }

        // Date securizate despre utilizator (Doar pentru afișare)
        public string? UserDisplayName { get; set; } // Va conține FirstName sau UserName
        public string? UserProfilePicture { get; set; } // Numele fișierului
        public string? UserName { get; set; } // Pentru link-uri sau fallback
    }
}