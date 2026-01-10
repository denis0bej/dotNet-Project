using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using dotNet_Project.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using dotNet_Project.Models;
using System.IO; // Necesar pentru lucrul cu fisiere (Path, Directory, FileStream)

namespace dotNet_Project.Controllers
{
    [Authorize]
    public class BookmarksController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<BookmarksController> _logger;
        private readonly IWebHostEnvironment _env;

        public BookmarksController(ApplicationDbContext context,
                               UserManager<ApplicationUser> userManager,
                               ILogger<BookmarksController> logger,
                               IWebHostEnvironment env)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _env = env;
        }

        // 1. LISTARE (Index) - COMBINAT: Search/Sort (V2) + Admin Logic (V1)
        [HttpGet]
        public async Task<IActionResult> Index(string searchString, string sortOrder)
        {
            var currentUserId = _userManager.GetUserId(User);
            var isAdmin = User.IsInRole("Admin");

            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentSort"] = sortOrder;

            // 1. Query de bază
            IQueryable<Bookmark> bookmarksQuery = _context.Bookmarks
                                                            .Include(b => b.Tags)
                                                            .Include(b => b.Categories)
                                                            .Include(b => b.Votes)
                                                            .Include(b => b.User);

            // LOGICA DIN VARIANTA 1 (Admin vede tot, User vede doar ale lui)
            if (!isAdmin)
            {
                bookmarksQuery = bookmarksQuery.Where(b => b.UserId == currentUserId);
            }

            // 2. LOGICA DE CĂUTARE AVANSATĂ (DIN VARIANTA 2)
            if (!string.IsNullOrEmpty(searchString))
            {
                // A. FILTRARE: Luăm tot ce se potrivește (Titlu OR Descriere OR Categorie)
                bookmarksQuery = bookmarksQuery.Where(b =>
                    b.Title.Contains(searchString) ||
                    (b.Description != null && b.Description.Contains(searchString)) ||
                    b.Categories.Any(c => c.Name.Contains(searchString)));

                // B. PRIORITIZARE (Ranking)
                bookmarksQuery = bookmarksQuery.OrderBy(b => b.Title.Contains(searchString) ? 0 : 1);

                // C. SORTARE SECUNDARĂ
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
                // 3. SORTARE STANDARD (Fără căutare activă)
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

            var bookmarks = await bookmarksQuery.ToListAsync();
            return View(bookmarks);
        }

        // 2. CREARE (New) - GET
        [HttpGet]
        [Authorize(Roles = "Admin,User")]
        public IActionResult New()
        {
            var currentUserId = _userManager.GetUserId(User);
            var userCategories = _context.Categories.Where(c => c.UserId == currentUserId);

            ViewBag.CategoryList = userCategories.Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.Name
            }).ToList();

            return View(new Bookmark());
        }

        // 2. CREARE (New) - POST
        [HttpPost]
        [Authorize(Roles = "Admin,User")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> New(Bookmark bookmark, int[] selectedCategories)
        {
            var userId = _userManager.GetUserId(User);
            bookmark.UserId = userId;
            bookmark.CreatedAt = DateTime.Now;

            // --- LOGICA UPLOAD IMAGINE ---
            if (bookmark.ImageUpload != null)
            {
                var storagePath = Path.Combine(_env.WebRootPath, "images", "bookmarks");
                if (!Directory.Exists(storagePath)) Directory.CreateDirectory(storagePath);

                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(bookmark.ImageUpload.FileName);
                var filePath = Path.Combine(storagePath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await bookmark.ImageUpload.CopyToAsync(stream);
                }

                bookmark.MediaUrl = "/images/bookmarks/" + fileName;
            }

            // Logica categorii
            if (selectedCategories != null)
            {
                foreach (var catId in selectedCategories)
                {
                    var category = await _context.Categories.FindAsync(catId);
                    if (category != null) bookmark.Categories.Add(category);
                }
            }

            // Validare Model
            ModelState.Remove("UserId");
            ModelState.Remove("Categories");
            ModelState.Remove("User");
            ModelState.Remove("Tags");
            ModelState.Remove("ImageUpload");

            if (ModelState.IsValid)
            {
                _context.Add(bookmark);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            // Reload ViewBag daca e eroare
            var userCategories = _context.Categories.Where(c => c.UserId == userId);
            ViewBag.CategoryList = userCategories.Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.Name
            }).ToList();

            return View(bookmark);
        }

        // 3. EDITARE (Edit) - GET
        [HttpGet]
        [Authorize(Roles = "Admin,User")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var bookmark = await _context.Bookmarks
                                         .Include(b => b.Tags)
                                         .Include(b => b.Categories)
                                         .FirstOrDefaultAsync(b => b.Id == id);

            if (bookmark == null) return NotFound();

            var currentUserId = _userManager.GetUserId(User);
            if (bookmark.UserId != currentUserId) return RedirectToAction(nameof(Index));

            var allUserCategories = _context.Categories.Where(c => c.UserId == currentUserId);
            var selectedIds = bookmark.Categories.Select(c => c.Id).ToList();

            ViewBag.CategoryList = allUserCategories.Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.Name,
                Selected = selectedIds.Contains(c.Id)
            }).ToList();

            return View(bookmark);
        }

        // 3. EDITARE (Edit) - POST
        [HttpPost]
        [Authorize(Roles = "Admin,User")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Bookmark bookmark, int[] selectedCategories)
        {
            if (id != bookmark.Id) return NotFound();

            var currentUserId = _userManager.GetUserId(User);

            var existingBookmark = await _context.Bookmarks
                                                 .Include(b => b.Categories)
                                                 .FirstOrDefaultAsync(b => b.Id == id);

            if (existingBookmark == null || existingBookmark.UserId != currentUserId)
            {
                return Forbid();
            }

            existingBookmark.Title = bookmark.Title;
            existingBookmark.Description = bookmark.Description;
            existingBookmark.IsPublic = bookmark.IsPublic;

            // --- LOGICA UPLOAD IMAGINE (Editare) ---
            if (bookmark.ImageUpload != null)
            {
                // Stergem vechea imagine (Helper din Varianta 1)
                DeleteLocalImage(existingBookmark.MediaUrl);

                var storagePath = Path.Combine(_env.WebRootPath, "images", "bookmarks");
                if (!Directory.Exists(storagePath)) Directory.CreateDirectory(storagePath);

                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(bookmark.ImageUpload.FileName);
                var filePath = Path.Combine(storagePath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await bookmark.ImageUpload.CopyToAsync(stream);
                }

                existingBookmark.MediaUrl = "/images/bookmarks/" + fileName;
            }
            else if (!string.IsNullOrEmpty(bookmark.MediaUrl))
            {
                existingBookmark.MediaUrl = bookmark.MediaUrl;
            }

            // Actualizare categorii
            existingBookmark.Categories.Clear();
            if (selectedCategories != null)
            {
                foreach (var catId in selectedCategories)
                {
                    var category = await _context.Categories.FindAsync(catId);
                    if (category != null) existingBookmark.Categories.Add(category);
                }
            }

            try
            {
                _context.Update(existingBookmark);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!BookmarkExists(bookmark.Id)) return NotFound();
                else throw;
            }
            return RedirectToAction(nameof(Index));
        }

        // 4. STERGERE (Delete) - GET
        [HttpGet]
        [Authorize(Roles = "Admin,User")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var bookmark = await _context.Bookmarks
                                         .Include(b => b.User)
                                         .Include(b => b.Categories)
                                         .FirstOrDefaultAsync(m => m.Id == id);

            if (bookmark == null) return NotFound();

            var currentUserId = _userManager.GetUserId(User);

            if (bookmark.UserId != currentUserId && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            return View(bookmark);
        }

        // 4. STERGERE (Delete) - POST
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var bookmark = await _context.Bookmarks.FindAsync(id);

            if (bookmark == null) return NotFound();

            var currentUserId = _userManager.GetUserId(User);

            if (bookmark.UserId != currentUserId && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            // Stergere imagine locală (Helper din Varianta 1)
            DeleteLocalImage(bookmark.MediaUrl);

            _context.Bookmarks.Remove(bookmark);
            await _context.SaveChangesAsync();
            await DeleteOrphanTags();

            return RedirectToAction(nameof(Index));
        }

        // 5. SHOW
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Show(int? id)
        {
            if (id == null) return NotFound();

            var bookmark = await _context.Bookmarks
                                         .Include(b => b.Tags)
                                         .Include(b => b.User)
                                         .Include(b => b.Categories)
                                         .Include(b => b.Votes)
                                         .Include(b => b.Comments).ThenInclude(c => c.User)
                                         .FirstOrDefaultAsync(m => m.Id == id);

            if (bookmark == null) return NotFound();

            var currentUserId = _userManager.GetUserId(User);
            var isAdmin = User.IsInRole("Admin");

            if (!bookmark.IsPublic)
            {
                if (bookmark.UserId != currentUserId && !isAdmin)
                {
                    if (currentUserId == null) return Challenge();
                    return Forbid();
                }
            }

            ViewData["CurrentUserProfilePic"] = null;
            if (currentUserId != null)
            {
                var currentUser = await _userManager.FindByIdAsync(currentUserId);
                ViewData["CurrentUserProfilePic"] = currentUser?.ProfilePicture;
            }

            return View(bookmark);
        }

        // 6. SaveToCollection
        [Authorize(Roles = "Admin,User")]
        public async Task<IActionResult> SaveToCollection(int? id)
        {
            if (id == null) return NotFound();
            var bookmark = await _context.Bookmarks.FindAsync(id);
            if (bookmark == null) return NotFound();

            var currentUserId = _userManager.GetUserId(User);
            var myCategories = _context.Categories
                                       .Where(c => c.UserId == currentUserId)
                                       .Include(c => c.Bookmarks);

            var categoriesList = await myCategories.ToListAsync();
            ViewBag.CategoryList = categoriesList.Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.Name,
                Selected = c.Bookmarks.Any(b => b.Id == bookmark.Id)
            }).ToList();

            return View(bookmark);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveToCollection(int id, int[] selectedCategories)
        {
            var bookmark = await _context.Bookmarks.FindAsync(id);
            if (bookmark == null) return NotFound();

            var currentUserId = _userManager.GetUserId(User);

            if (selectedCategories != null)
            {
                var myCategories = await _context.Categories
                                                 .Include(c => c.Bookmarks)
                                                 .Where(c => c.UserId == currentUserId)
                                                 .ToListAsync();

                foreach (var category in myCategories)
                {
                    if (selectedCategories.Contains(category.Id))
                    {
                        if (!category.Bookmarks.Any(b => b.Id == id)) category.Bookmarks.Add(bookmark);
                    }
                    else
                    {
                        var existingLink = category.Bookmarks.FirstOrDefault(b => b.Id == id);
                        if (existingLink != null) category.Bookmarks.Remove(existingLink);
                    }
                }
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("Index");
        }

        // 7. ToggleLike
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> ToggleLike([FromBody] int id)
        {
            var bookmark = await _context.Bookmarks.FindAsync(id);
            if (bookmark == null) return NotFound();

            var currentUserId = _userManager.GetUserId(User);
            var existingVote = await _context.Votes.FirstOrDefaultAsync(v => v.BookmarkId == id && v.UserId == currentUserId);

            bool isLiked;
            if (existingVote != null)
            {
                _context.Votes.Remove(existingVote);
                if (bookmark.LikeCount > 0) bookmark.LikeCount--;
                isLiked = false;
            }
            else
            {
                var newVote = new Vote { BookmarkId = id, UserId = currentUserId };
                _context.Votes.Add(newVote);
                bookmark.LikeCount++;
                isLiked = true;
            }
            await _context.SaveChangesAsync();
            return Json(new { success = true, newCount = bookmark.LikeCount, isLiked = isLiked });
        }

        // 8. DeleteAjax (Specific Varianta 2)
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAjax(int id)
        {
            var bookmark = await _context.Bookmarks.FindAsync(id);

            if (bookmark == null)
            {
                return Json(new { success = false, message = "Postarea nu a fost găsită." });
            }

            // Stergem si poza locala daca exista (folosim helperul)
            DeleteLocalImage(bookmark.MediaUrl);

            _context.Bookmarks.Remove(bookmark);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Postarea a fost ștearsă cu succes!" });
        }

        // --- HELPERS ---

        private async Task DeleteOrphanTags()
        {
            var orphanTags = await _context.Tags.Where(t => !t.Bookmarks.Any()).ToListAsync();
            if (orphanTags.Any())
            {
                _context.Tags.RemoveRange(orphanTags);
                await _context.SaveChangesAsync();
            }
        }

        private bool BookmarkExists(int id)
        {
            return _context.Bookmarks.Any(e => e.Id == id);
        }

        // Helper din Varianta 1 (ADĂUGAT AICI PENTRU A FI FOLOSIT ÎN EDIT/DELETE)
        private void DeleteLocalImage(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return;

            // Verificăm dacă e fișier local (începe cu /images/) și nu e link extern (YouTube)
            if (relativePath.StartsWith("/images/"))
            {
                // Construim calea absolută pe disc (ex: C:\Proiect\wwwroot\images\bookmarks\poza.jpg)
                // TrimStart('/') scoate primul slash pentru a combina corect calea
                var absolutePath = Path.Combine(_env.WebRootPath, relativePath.TrimStart('/'));

                if (System.IO.File.Exists(absolutePath))
                {
                    try
                    {
                        System.IO.File.Delete(absolutePath);
                    }
                    catch (Exception ex)
                    {
                        // Putem loga eroarea, dar nu vrem să oprim execuția programului
                        _logger.LogWarning("Nu s-a putut sterge fisierul: " + absolutePath + " Error: " + ex.Message);
                    }
                }
            }
        }
    }
}