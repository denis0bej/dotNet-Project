using dotNet_Project.Data;
using dotNet_Project.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace dotNet_Project.Controllers
{
    public class CategoriesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public CategoriesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [Authorize(Roles = "Admin,User")]
        public async Task<IActionResult> Index()
        {
            var currentUserId = _userManager.GetUserId(User);

            // Adminul vede toate categoriile
            if (User.IsInRole("Admin"))
            {
                return View(await _context.Categories.Include(c => c.User).ToListAsync());
            }
            else
            {
                // Userul vede doar categoriile proprii
                return View(await _context.Categories
                                    .Where(c => c.UserId == currentUserId)
                                    .ToListAsync());
            }
        }

        // GET: Categories/New
        public IActionResult New()
        {
            return View();
        }

        // POST: Categories/New
        [HttpPost]
        [Authorize(Roles = "Admin,User")] // Trebuie sa fie autentificat
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> New([Bind("Id,Name,IsPublic")] Category category)
        {
            ModelState.Remove("UserId");
            ModelState.Remove("User");
            ModelState.Remove("Bookmarks");

            if (ModelState.IsValid)
            {
                category.UserId = _userManager.GetUserId(User);

                _context.Add(category);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(category);
        }

        // GET: Categories/Edit/5
        [Authorize(Roles = "Admin,User")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var category = await _context.Categories.FindAsync(id);
            if (category == null) return NotFound();

            var currentUserId = _userManager.GetUserId(User);
            // Daca nu e userul specific, nu are voie sa editeze (chiar daca e admin)
            if (category.UserId != currentUserId)
            {
                return RedirectToAction("Index");
            }

            return View(category);
        }

        // POST: Categories/Edit/5
        [HttpPost]
        [Authorize(Roles = "Admin,User")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,IsPublic")] Category category)
        {
            if (id != category.Id) return NotFound();

            // Preluam categoria originala din baza de date pentru a nu pierde UserId-ul
            var originalCategory = await _context.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);

            var currentUserId = _userManager.GetUserId(User);
            if (originalCategory.UserId != currentUserId)
            {
                return RedirectToAction("Index");
            }

            ModelState.Remove("UserId");
            ModelState.Remove("User");
            ModelState.Remove("Bookmarks");

            if (ModelState.IsValid)
            {
                try
                {
                    // Asiguram ca UserId ramane cel original
                    category.UserId = originalCategory.UserId;

                    _context.Update(category);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Categories.Any(e => e.Id == category.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(category);
        }

        // GET: Categories/Delete/5
        [Authorize(Roles = "Admin,User")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var category = await _context.Categories
                .Include(c => c.User)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (category == null) return NotFound();

            var currentUserId = _userManager.GetUserId(User);
            if (category.UserId != currentUserId && !User.IsInRole("Admin"))
            {
                RedirectToAction("AccessDenied");
            }

            return View(category);
        }

        // POST: Categories/Delete/5
        [HttpPost, ActionName("Delete")]
        [Authorize(Roles = "Admin,User")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var category = await _context.Categories.FindAsync(id);

            // Verificare securitate finala
            var currentUserId = _userManager.GetUserId(User);
            if (category.UserId != currentUserId && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            if (category != null)
            {
                _context.Categories.Remove(category);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        // GET: Categories/Profile/userid-string-aici
        public async Task<IActionResult> Profile(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                // Daca nu se specifica un id, redirectionam catre profilul utilizatorului curent
                userId = _userManager.GetUserId(User);
            }

            if (userId == null) return NotFound();

            var currentUserId = _userManager.GetUserId(User);
            var isAdmin = User.IsInRole("Admin");

            // Selectam categoriile utilizatorului pe al carui profil ne aflăm
            var query = _context.Categories
                                .Include(c => c.User)
                                .Where(c => c.UserId == userId);

            // Aplicam logica de vizibilitate:
            if (currentUserId != userId && !isAdmin)
            {
                query = query.Where(c => c.IsPublic == true);
            }

            var categories = await query.ToListAsync();

            // (Optional) Gasim numele utilizatorului pentru a-l afișa în titlu
            var userProfile = await _userManager.FindByIdAsync(userId);
            ViewData["UserProfileName"] = userProfile?.UserName ?? "User";
            ViewData["IsOwner"] = (currentUserId == userId);

            return View(categories);
        }

        // GET: Categories/Show/5
        public async Task<IActionResult> Show(int? id)
        {
            if (id == null) return NotFound();

            var currentUserId = _userManager.GetUserId(User);
            var isAdmin = User.IsInRole("Admin");

            // Incarcam Categoria IMPREUNA cu Bookmark-urile si User-ul care a creat bookmark-ul
            var category = await _context.Categories
                                         .Include(c => c.User)
                                         .Include(c => c.Bookmarks)
                                         .ThenInclude(b => b.User) // Ca sa afisam autorul bookmark-ului
                                         .FirstOrDefaultAsync(m => m.Id == id);

            if (category == null) return NotFound();

            // --- VERIFICARI SECURITATE CATEGORIE (FOLDER) ---

            // 1. Daca categoria e privata si nu e a mea (si nu sunt admin) -> Nu am voie
            if (category.UserId != currentUserId && !category.IsPublic && !isAdmin)
            {
                return Forbid();
            }

            // --- FILTRARE CONTINUT (Ce bookmark-uri vad in folder?) ---

            // Daca sunt proprietarul folderului sau Admin -> Vad toate bookmark-urile (si cele private)
            // Daca sunt vizitator -> Vad doar bookmark-urile PUBLICE din aceasta categorie
            if (category.UserId != currentUserId && !isAdmin)
            {
                category.Bookmarks = category.Bookmarks.Where(b => b.IsPublic == true).ToList();
            }

            // Trimitem flag-uri catre View pentru butoane
            ViewData["IsOwner"] = (category.UserId == currentUserId);
            ViewData["IsAdmin"] = isAdmin;

            return View(category);
        }

    }
}
