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
        // Parametrul 'searchString' vine din input-ul formularului pe care il vom face
        public async Task<IActionResult> Index(string sortOrder, string searchString)
        {
            // Salvăm parametrii în ViewData pentru a-i păstra în View (ex: să rămână textul în căsuța de search)
            ViewData["CurrentSort"] = sortOrder;
            ViewData["CurrentFilter"] = searchString;

            // 1. Construim interogarea de bază
            var bookmarksQuery = _context.Bookmarks
                                         .Include(b => b.Categories)
                                         .Include(b => b.User)
                                         .Include(b => b.Votes)
                                         .Where(b => b.IsPublic) // Doar cele publice
                                         .AsQueryable();

            // 2. Aplicăm FILTRAREA (Căutarea)
            if (!string.IsNullOrEmpty(searchString))
            {
                // Căutăm parțial (Contains) în Titlu, Descriere SAU în Numele Categoriilor
                bookmarksQuery = bookmarksQuery.Where(b =>
                    b.Title.Contains(searchString) ||
                    b.Description.Contains(searchString) ||
                    b.Categories.Any(c => c.Name.Contains(searchString)));
            }

            // 3. Aplicăm SORTAREA
            switch (sortOrder)
            {
                case "Popular":
                    bookmarksQuery = bookmarksQuery.OrderByDescending(b => b.LikeCount);
                    break;
                case "Recent":
                default:
                    bookmarksQuery = bookmarksQuery.OrderByDescending(b => b.CreatedAt);
                    break;
            }

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