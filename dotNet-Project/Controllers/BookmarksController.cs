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

        // 1. LISTARE (Index) - DOAR COLECȚIA MEA (Private + Publice)
        [HttpGet]
        public async Task<IActionResult> Index(string searchString)
        {
            var currentUserId = _userManager.GetUserId(User);

            ViewData["CurrentFilter"] = searchString;

            // 1. Query de bază: Luăm DOAR bookmark-urile utilizatorului curent
            IQueryable<Bookmark> bookmarksQuery = _context.Bookmarks
                                                            .Include(b => b.Tags)
                                                            .Include(b => b.Categories)
                                                            .Include(b => b.Votes)
                                                            .Include(b => b.User)
                                                            .Where(b => b.UserId == currentUserId); // <--- SCHIMBAREA CHEIE (Doar ale mele)

            // 2. LOGICA DE CAUTARE (În colecția proprie)
            if (!string.IsNullOrEmpty(searchString))
            {
                bookmarksQuery = bookmarksQuery.Where(b =>
                    b.Title.Contains(searchString) ||
                    (b.Description != null && b.Description.Contains(searchString)) ||
                    b.Categories.Any(c => c.Name.Contains(searchString)));
            }
            else
            {
                // MODIFICARE AICI: Stergem "|| b.IsPublic == true"
                // Acum userul vede DOAR bookmark-urile create de el
                bookmarksQuery = bookmarksQuery.Where(b => b.UserId == currentUserId);
            }

            // 3. Ordonare: Cele mai noi sus
            var bookmarks = await bookmarksQuery.OrderByDescending(b => b.CreatedAt).ToListAsync();

            return View(bookmarks);
        }

        // 2. CREARE (New) - Formularul
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
                // Definim calea: wwwroot/images/bookmarks
                var storagePath = Path.Combine(_env.WebRootPath, "images", "bookmarks");

                // Cream folderul daca nu exista
                if (!Directory.Exists(storagePath)) Directory.CreateDirectory(storagePath);

                // Generam nume unic
                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(bookmark.ImageUpload.FileName);
                var filePath = Path.Combine(storagePath, fileName);

                // Salvam fisierul fizic
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await bookmark.ImageUpload.CopyToAsync(stream);
                }

                // Setam proprietatea MediaUrl cu calea relativa
                bookmark.MediaUrl = "/images/bookmarks/" + fileName;
            }
            // ------------------------------

            // Logica categorii (ramane la fel)
            if (selectedCategories != null)
            {
                foreach (var catId in selectedCategories)
                {
                    var category = await _context.Categories.FindAsync(catId);
                    if (category != null) bookmark.Categories.Add(category);
                }
            }

            ModelState.Remove("UserId");
            ModelState.Remove("Categories");
            ModelState.Remove("User");
            ModelState.Remove("Tags");
            ModelState.Remove("ImageUpload"); // Scoatem din validare ca nu e in DB

            if (ModelState.IsValid)
            {
                _context.Add(bookmark);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            var userCategories = _context.Categories.Where(c => c.UserId == userId);
            ViewBag.CategoryList = userCategories.Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.Name
            }).ToList();

            return View(bookmark);
        }

        [HttpGet]
        [Authorize(Roles = "Admin,User")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var bookmark = await _context.Bookmarks.Include(b => b.Tags).Include(b => b.Categories).FirstOrDefaultAsync(b => b.Id == id);
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

        [HttpPost]
        [Authorize(Roles = "Admin,User")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Bookmark bookmark, int[] selectedCategories)
        {
            if (id != bookmark.Id) return NotFound();
            var currentUserId = _userManager.GetUserId(User);

            // Incarcam bookmark-ul existent
            var existingBookmark = await _context.Bookmarks
                                                 .Include(b => b.Categories)
                                                 .FirstOrDefaultAsync(b => b.Id == id);

            if (existingBookmark == null || existingBookmark.UserId != currentUserId)
            {
                return Forbid();
            }

            // Actualizam datele simple
            existingBookmark.Title = bookmark.Title;
            existingBookmark.Description = bookmark.Description;
            existingBookmark.IsPublic = bookmark.IsPublic;

            // --- LOGICA UPLOAD IMAGINE (Editare) ---
            if (bookmark.ImageUpload != null)
            {
                // Daca userul incarca o poza noua, o procesam
                var storagePath = Path.Combine(_env.WebRootPath, "images", "bookmarks");
                if (!Directory.Exists(storagePath)) Directory.CreateDirectory(storagePath);

                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(bookmark.ImageUpload.FileName);
                var filePath = Path.Combine(storagePath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await bookmark.ImageUpload.CopyToAsync(stream);
                }

                // Suprascriem vechiul URL cu noua imagine
                existingBookmark.MediaUrl = "/images/bookmarks/" + fileName;
            }
            else if (!string.IsNullOrEmpty(bookmark.MediaUrl))
            {
                // Daca NU a incarcat poza noua, dar a schimbat textul URL-ului (ex: link YouTube), il actualizam
                // Atentie: Aici decidem prioritatea. De obicei, daca da upload, ignoram URL-ul text.
                existingBookmark.MediaUrl = bookmark.MediaUrl;
            }
            // ---------------------------------------

            // Actualizare categorii (codul existent ramane la fel)
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

        [HttpGet]
        [Authorize(Roles = "Admin,User")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var bookmark = await _context.Bookmarks.Include(b => b.User).Include(b => b.Categories).FirstOrDefaultAsync(m => m.Id == id);
            if (bookmark == null) return NotFound();
            var currentUserId = _userManager.GetUserId(User);
            if (bookmark.UserId != currentUserId && !User.IsInRole("Admin")) return Forbid();
            return View(bookmark);
        }

        [HttpPost, ActionName("Delete")]
        [Authorize(Roles = "Admin,User")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var bookmark = await _context.Bookmarks.FindAsync(id);
            if (bookmark == null) return NotFound();
            var currentUserId = _userManager.GetUserId(User);
            if (bookmark.UserId != currentUserId && !User.IsInRole("Admin")) return Forbid();
            _context.Bookmarks.Remove(bookmark);
            await _context.SaveChangesAsync();
            await DeleteOrphanTags();
            return RedirectToAction(nameof(Index));
        }

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
                                         // Includem User-ul comentariului pentru a avea acces la ProfilePicture
                                         .Include(b => b.Comments).ThenInclude(c => c.User)
                                         .FirstOrDefaultAsync(m => m.Id == id);
            if (bookmark == null) return NotFound();

            // --- LOGICA DE VIZIBILITATE (existenta) ---
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
            // ----------------------------

            return View(bookmark);
        }

        [Authorize(Roles = "Admin,User")]
        public async Task<IActionResult> SaveToCollection(int? id)
        {
            if (id == null) return NotFound();
            var bookmark = await _context.Bookmarks.FindAsync(id);
            if (bookmark == null) return NotFound();
            var currentUserId = _userManager.GetUserId(User);
            var myCategories = _context.Categories.Where(c => c.UserId == currentUserId).Include(c => c.Bookmarks);
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
                var myCategories = await _context.Categories.Include(c => c.Bookmarks).Where(c => c.UserId == currentUserId).ToListAsync();
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
    }
}