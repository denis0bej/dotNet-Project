using System.Diagnostics;
using dotNet_Project.Data;       // Acces la DB
using dotNet_Project.Models;     // Acces la Modele
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore; // Pentru Include, ToListAsync etc.

namespace dotNet_Project.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        // Injectăm Contextul și UserManager-ul prin constructor
        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _logger = logger;
            _context = context;
            _userManager = userManager;
        }

        // Metoda Index primește un parametru opțional "sortOrder"
        public async Task<IActionResult> Index(string sortOrder)
        {
            // Setăm filtrul curent în ViewBag pentru a ști ce buton să evidențiem în View
            ViewBag.CurrentSort = sortOrder;

            // 1. Interogarea de bază:
            // - Luăm bookmark-urile
            // - Includem Categoria (să verificăm dacă e publică)
            // - Includem User-ul (să afișăm cine a postat)
            // - Includem Voturile (să vedem dacă am dat like)
            var bookmarksQuery = _context.Bookmarks
                                         .Include(b => b.Category)
                                         .Include(b => b.User)
                                         .Include(b => b.Votes)
                                         .Where(b => b.Category.IsPublic == true); // DOAR cele publice

            // 2. Aplicăm Filtrarea (Sortarea)
            switch (sortOrder)
            {
                case "Popular":
                    // Ordonăm descrescător după numărul de like-uri
                    bookmarksQuery = bookmarksQuery.OrderByDescending(b => b.LikeCount);
                    break;

                case "Recent":
                default:
                    // Default: Cele mai noi primele
                    bookmarksQuery = bookmarksQuery.OrderByDescending(b => b.CreatedAt);
                    break;
            }

            // 3. Executăm interogarea
            var bookmarks = await bookmarksQuery.ToListAsync();

            return View(bookmarks);
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