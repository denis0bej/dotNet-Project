using System.Collections.Generic;

namespace dotNet_Project.Models
{
    public class UserProfileViewModel
    {
        public string UserId { get; set; }
        public string FullName { get; set; }
        public string Description { get; set; }
        public string ProfilePicture { get; set; }
        public bool IsOwner { get; set; }
        public List<Category> Categories { get; set; } = new List<Category>();
        public List<Bookmark> Bookmarks { get; set; } = new List<Bookmark>();
    }
}