using dotNet_Project.Data;
using dotNet_Project.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using dotNet_Project.Models;

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
        // În CategoriesController.cs

        // GET: Categories/Show/5
        public async Task<IActionResult> Show(int? id, int? pageNumber)
        {
            if (id == null) return NotFound();

            var currentUserId = _userManager.GetUserId(User);
            var isAdmin = User.IsInRole("Admin");
            int pageSize = 15; // Maxim 15 pe pagină
            int pageIndex = pageNumber ?? 1;

            // 1. Încărcăm doar DETALIILE Categoriei (fără bookmark-uri deocamdată)
            var category = await _context.Categories
                                         .Include(c => c.User)
                                         .FirstOrDefaultAsync(m => m.Id == id);

            if (category == null) return NotFound();

            // --- VERIFICĂRI SECURITATE ---
            // Dacă categoria e privată și nu e a mea (și nu sunt admin) -> Nu am voie
            if (category.UserId != currentUserId && !category.IsPublic && !isAdmin)
            {
                return Forbid();
            }

            // 2. Construim Query-ul pentru Bookmark-uri din această categorie
            var bookmarksQuery = _context.Bookmarks
                                         .Include(b => b.Categories) // Necesar pentru badge-uri
                                         .Include(b => b.Votes)
                                         .Include(b => b.User)
                                         .Where(b => b.Categories.Any(c => c.Id == id)) // Doar din categoria curentă
                                         .AsQueryable();

            // 3. Filtrare Conținut (Ce bookmark-uri văd în folder?)
            // Dacă nu sunt proprietar/admin, văd doar bookmark-urile publice din acest folder
            if (category.UserId != currentUserId && !isAdmin)
            {
                bookmarksQuery = bookmarksQuery.Where(b => b.IsPublic == true);
            }

            // Sortare
            bookmarksQuery = bookmarksQuery.OrderByDescending(b => b.CreatedAt);

            // 4. PAGINARE
            var totalItems = await bookmarksQuery.CountAsync();
            var pagedList = await bookmarksQuery
                                    .Skip((pageIndex - 1) * pageSize)
                                    .Take(pageSize)
                                    .ToListAsync();

            // 5. Mapare la ViewModel (Carduri)
            var bookmarkViewModels = pagedList.Select(b =>
            {
                var appUser = b.User as ApplicationUser;
                return new BookmarkCardViewModel
                {
                    Id = b.Id,
                    Title = b.Title,
                    MediaUrl = b.MediaUrl,
                    LikeCount = b.Votes.Count,
                    IsPublic = b.IsPublic,
                    IsLikedByCurrentUser = currentUserId != null && b.Votes.Any(v => v.UserId == currentUserId),
                    CategoryNames = b.Categories.Select(c => c.Name).ToList(),
                    UserDisplayName = !string.IsNullOrEmpty(appUser?.FirstName) ? appUser.FirstName : (b.User?.UserName ?? "Unknown"),
                    UserProfilePicture = appUser?.ProfilePicture,
                    UserName = b.User?.UserName
                };
            }).ToList();

            var paginatedBookmarks = new PaginatedList<BookmarkCardViewModel>(bookmarkViewModels, totalItems, pageIndex, pageSize);

            // 6. Construim Modelul Final
            var model = new CategoryDetailsViewModel
            {
                Id = category.Id,
                Name = category.Name,
                IsPublic = category.IsPublic,
                UserId = category.UserId,
                UserDisplayName = category.User?.UserName ?? "Unknown",
                Bookmarks = paginatedBookmarks
            };

            ViewData["IsOwner"] = (category.UserId == currentUserId);

            return View(model);
        }

    }
}
