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

        public async Task<IActionResult> Index(string sortOrder)
        {
            ViewBag.CurrentSort = sortOrder;

            // Obținem ID-ul utilizatorului curent (poate fi null dacă nu e logat)
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // 1. Construim interogarea (Luăm datele brute din DB)
            var bookmarksQuery = _context.Bookmarks
                                         .Include(b => b.Categories)
                                         .Include(b => b.User)
                                         .Include(b => b.Votes)
                                         .Where(b => b.IsPublic) // Doar cele publice
                                         .AsQueryable();

            // 2. Aplicăm Sortarea
            switch (sortOrder)
            {
                case "Popular":
                    bookmarksQuery = bookmarksQuery.OrderByDescending(b => b.LikeCount); // Asumând că ai proprietatea LikeCount calculată sau o iei din Votes.Count
                    break;
                case "Recent":
                default:
                    bookmarksQuery = bookmarksQuery.OrderByDescending(b => b.CreatedAt);
                    break;
            }

            // Executăm interogarea la baza de date
            var bookmarksList = await bookmarksQuery.ToListAsync();

            // 3. MAPAREA SECURE (Transformăm în ViewModel)
            // Aici transformăm obiectele complexe de DB în obiecte simple pentru View
            var viewModelList = bookmarksList.Select(b =>
            {
                // Facem cast sigur la ApplicationUser
                var appUser = b.User as ApplicationUser;

                return new BookmarkCardViewModel
                {
                    Id = b.Id,
                    Title = b.Title,
                    MediaUrl = b.MediaUrl,
                    LikeCount = b.Votes.Count, // Calculăm like-urile

                    // Verificăm dacă userul curent a dat like
                    IsLikedByCurrentUser = currentUserId != null && b.Votes.Any(v => v.UserId == currentUserId),

                    CategoryNames = b.Categories.Select(c => c.Name).ToList(),

                    // LOGICA DE AFIȘARE USER (Securizată)
                    // Dacă are prenume, îl folosim, altfel folosim username-ul
                    UserDisplayName = !string.IsNullOrEmpty(appUser?.FirstName) ? appUser.FirstName : (b.User?.UserName ?? "Unknown"),

                    // Poză de profil
                    UserProfilePicture = appUser?.ProfilePicture,
                    UserName = b.User?.UserName
                };
            }).ToList();

            // Trimitem lista de ViewModels la View
            return View(viewModelList);
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