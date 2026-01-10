using System.Collections.Generic;

namespace dotNet_Project.Models
{
    public class UserProfileViewModel
    {
        public string UserId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Description { get; set; }
        public string ProfilePicture { get; set; }
        public bool IsOwner { get; set; }
        public PaginatedList<Category> Categories { get; set; }
        public PaginatedList<BookmarkCardViewModel> Bookmarks { get; set; }
    }
}