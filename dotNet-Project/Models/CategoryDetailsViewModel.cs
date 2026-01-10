using dotNet_Project.Models;

namespace dotNet_Project.Models
{
    public class CategoryDetailsViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public bool IsPublic { get; set; }
        public string UserId { get; set; }
        public string UserDisplayName { get; set; }

        // Aici avem lista paginată de carduri, exact ca în Feed
        public PaginatedList<BookmarkCardViewModel> Bookmarks { get; set; }
    }
}