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

        // INDEX COMBINAT: Search/Sort (V2) + Paginare/ViewModel (V1)
        public async Task<IActionResult> Index(string searchString, string sortOrder, int? pageNumber)
        {
            // Salvăm parametrii în View (pentru butoane și link-uri de paginare)
            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentSort"] = sortOrder;

            int pageSize = 15; // Maxim 15 bookmark-uri per pagină
            int pageIndex = pageNumber ?? 1;

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // 1. QUERY DE BAZĂ (Doar Publice)
            IQueryable<Bookmark> bookmarksQuery = _context.Bookmarks
                                         .Include(b => b.User)
                                         .Include(b => b.Votes)
                                         .Include(b => b.Categories)
                                         .Where(b => b.IsPublic);

            // 2. LOGICA DE CĂUTARE AVANSATĂ (Din Varianta 2)
            if (!string.IsNullOrEmpty(searchString))
            {
                // A. FILTRARE
                bookmarksQuery = bookmarksQuery.Where(b =>
                    b.Title.Contains(searchString) ||
                    (b.Description != null && b.Description.Contains(searchString)) ||
                    b.Categories.Any(c => c.Name.Contains(searchString)));

                // B. PRIORITIZARE (Ranking: Titlu > Descriere)
                bookmarksQuery = bookmarksQuery.OrderBy(b => b.Title.Contains(searchString) ? 0 : 1);

                // C. SORTARE SECUNDARĂ (ThenBy)
                switch (sortOrder)
                {
                    case "Popular":
                        bookmarksQuery = ((IOrderedQueryable<Bookmark>)bookmarksQuery).ThenByDescending(b => b.LikeCount);
                        break;
                    case "Recent": // Am uniformizat "Recent" cu "Date"
                    case "Date":
                    default:
                        bookmarksQuery = ((IOrderedQueryable<Bookmark>)bookmarksQuery).ThenByDescending(b => b.CreatedAt);
                        break;
                }
            }
            else
            {
                // 3. SORTARE STANDARD (Fără căutare)
                switch (sortOrder)
                {
                    case "Popular":
                        bookmarksQuery = bookmarksQuery.OrderByDescending(b => b.LikeCount);
                        break;
                    case "Recent":
                    case "Date":
                    default:
                        bookmarksQuery = bookmarksQuery.OrderByDescending(b => b.CreatedAt);
                        break;
                }
            }

            // 4. PAGINARE (Din Varianta 1)
            // Numărăm totalul după filtrare, dar înainte de paginare
            var totalItems = await bookmarksQuery.CountAsync();

            // Luăm doar "felia" necesară din baza de date
            var dbList = await bookmarksQuery
                                .Skip((pageIndex - 1) * pageSize)
                                .Take(pageSize)
                                .ToListAsync();

            // 5. MAPARE ÎN VIEWMODEL (Din Varianta 1)
            // Transformăm datele brute în ViewModel pentru a avea UserProfilePicture, IsLikedByCurrentUser etc.
            var viewModelList = dbList.Select(b =>
            {
                var appUser = b.User as ApplicationUser;
                return new BookmarkCardViewModel
                {
                    Id = b.Id,
                    Title = b.Title,
                    Description = b.Description, // Important pentru căutare
                    MediaUrl = b.MediaUrl,
                    LikeCount = b.Votes.Count,
                    IsLikedByCurrentUser = currentUserId != null && b.Votes.Any(v => v.UserId == currentUserId),
                    CategoryNames = b.Categories.Select(c => c.Name).ToList(),
                    IsPublic = b.IsPublic,
                    // Date User
                    UserDisplayName = !string.IsNullOrEmpty(appUser?.FirstName) ? appUser.FirstName : (b.User?.UserName ?? "Unknown"),
                    UserProfilePicture = appUser?.ProfilePicture,
                    UserName = b.User?.UserName,
                    CreatedAt = b.CreatedAt
                };
            }).ToList();

            // 6. Returnăm lista paginată
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