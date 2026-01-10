using System.Diagnostics;
using System.Security.Claims; // Necesar pentru User.FindFirstValue
using dotNet_Project.Data;
using dotNet_Project.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace dotNet_Project.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _logger = logger;
            _context = context;
            _userManager = userManager;
        }

        // În HomeController.cs

        public async Task<IActionResult> Index(string sortOrder, int? pageNumber)
        {
            ViewBag.CurrentSort = sortOrder;
            int pageSize = 15; // Maxim 15 bookmark-uri per pagină
            int pageIndex = pageNumber ?? 1; // Pagina curentă (default 1)

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // 1. Construim interogarea (Query-ul de bază)
            var bookmarksQuery = _context.Bookmarks
                                         .Include(b => b.Categories)
                                         .Include(b => b.User)
                                         .Include(b => b.Votes)
                                         .Where(b => b.IsPublic)
                                         .AsQueryable();

            // 2. Aplicăm Sortarea
            switch (sortOrder)
            {
                case "Popular":
                    bookmarksQuery = bookmarksQuery.OrderByDescending(b => b.Votes.Count);
                    break;
                case "Recent":
                default:
                    bookmarksQuery = bookmarksQuery.OrderByDescending(b => b.CreatedAt);
                    break;
            }

            // 3. PAGINAREA LA NIVEL DE BAZĂ DE DATE
            // Mai întâi numărăm totalul pentru a calcula numărul de pagini
            var totalItems = await bookmarksQuery.CountAsync();

            // Luăm doar elementele pentru pagina curentă
            var dbList = await bookmarksQuery
                                .Skip((pageIndex - 1) * pageSize)
                                .Take(pageSize)
                                .ToListAsync();

            // 4. Maparea datelor (Transformăm doar cele 15 elemente în ViewModel)
            var viewModelList = dbList.Select(b =>
            {
                var appUser = b.User as ApplicationUser;
                return new BookmarkCardViewModel
                {
                    Id = b.Id,
                    Title = b.Title,
                    MediaUrl = b.MediaUrl,
                    LikeCount = b.Votes.Count,
                    IsLikedByCurrentUser = currentUserId != null && b.Votes.Any(v => v.UserId == currentUserId),
                    CategoryNames = b.Categories.Select(c => c.Name).ToList(),
                    UserDisplayName = !string.IsNullOrEmpty(appUser?.FirstName) ? appUser.FirstName : (b.User?.UserName ?? "Unknown"),
                    UserProfilePicture = appUser?.ProfilePicture,
                    UserName = b.User?.UserName
                };
            }).ToList();

            // 5. Creăm obiectul paginat final
            var paginatedResult = new PaginatedList<BookmarkCardViewModel>(viewModelList, totalItems, pageIndex, pageSize);

            return View(paginatedResult);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}