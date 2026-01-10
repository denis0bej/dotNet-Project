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
        // Parametrul 'searchString' vine din input-ul formularului
        public async Task<IActionResult> Index(string searchString, string sortOrder)
        {
            // Salvăm parametrii pentru View (ca să știm ce buton să colorăm)
            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentSort"] = sortOrder;

            // 1. Query de bază: Luăm DOAR postările PUBLICE
            IQueryable<Bookmark> bookmarksQuery = _context.Bookmarks
                                         .Include(b => b.User)
                                         .Include(b => b.Votes)
                                         .Include(b => b.Categories)
                                         .Where(b => b.IsPublic); // <--- Doar cele publice

            // 2. LOGICA DE CĂUTARE AVANSATĂ (Weighted Search)
            if (!string.IsNullOrEmpty(searchString))
            {
                // A. FILTRARE: Luăm tot ce se potrivește (Titlu OR Descriere OR Categorie)
                bookmarksQuery = bookmarksQuery.Where(b =>
                    b.Title.Contains(searchString) ||
                    (b.Description != null && b.Description.Contains(searchString)) ||
                    b.Categories.Any(c => c.Name.Contains(searchString)));

                // B. PRIORITIZARE (Ranking)
                // Dacă Titlul conține textul, primește prioritate 0 (Maximă). Altfel 1.
                bookmarksQuery = bookmarksQuery.OrderBy(b => b.Title.Contains(searchString) ? 0 : 1);

                // C. SORTARE SECUNDARĂ (În funcție de preferința utilizatorului)
                // Folosim "ThenBy" pentru a nu strica prioritatea titlului
                switch (sortOrder)
                {
                    case "Popular":
                        bookmarksQuery = ((IOrderedQueryable<Bookmark>)bookmarksQuery).ThenByDescending(b => b.LikeCount);
                        break;
                    case "Date":
                    default:
                        bookmarksQuery = ((IOrderedQueryable<Bookmark>)bookmarksQuery).ThenByDescending(b => b.CreatedAt);
                        break;
                }
            }
            else
            {
                // 3. SORTARE STANDARD (Când nu căutăm nimic)
                // Aici folosim OrderBy simplu
                switch (sortOrder)
                {
                    case "Popular":
                        bookmarksQuery = bookmarksQuery.OrderByDescending(b => b.LikeCount);
                        break;
                    case "Date":
                    default:
                        bookmarksQuery = bookmarksQuery.OrderByDescending(b => b.CreatedAt);
                        break;
                }
            }

            // 4. Returnare listă
            return View(await bookmarksQuery.ToListAsync());
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