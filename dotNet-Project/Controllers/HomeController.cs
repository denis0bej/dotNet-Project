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
            ViewBag.CurrentSort = sortOrder;

            // 1. Construim interogarea
            var bookmarksQuery = _context.Bookmarks
                                         .Include(b => b.Categories) // <--- FOLOSIM LISTA (M-M)
                                         .Include(b => b.User)
                                         .Include(b => b.Votes)
                                         .Where(b => b.IsPublic) // Afișăm doar bookmark-urile publice
                                         .AsQueryable();


            // 3. Aplicăm Sortarea
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