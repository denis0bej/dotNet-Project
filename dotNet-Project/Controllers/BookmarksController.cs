using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using dotNet_Project.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using dotNet_Project.Models;

namespace dotNet_Project.Controllers
{
    public class BookmarksController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<BookmarksController> _logger;

        public BookmarksController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, ILogger<BookmarksController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // 1. LISTARE (Index)
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var currentUserId = _userManager.GetUserId(User);
            var isAdmin = User.IsInRole("Admin");

            IQueryable<Bookmark> bookmarksQuery = _context.Bookmarks
                                                          .Include(b => b.Tags);

            if (isAdmin)
            {
                // Adminul vede TOT (indiferent de owner sau vizibilitate) [cite: 2]
                // Nu aplicam niciun filtru Where
            }
            else
            {
                // Userul obisnuit vede: Ale sale SAU Cele Publice [cite: 1]
                bookmarksQuery = bookmarksQuery.Where(b => b.UserId == currentUserId || b.IsPublic == true);
            }

            var bookmarks = await bookmarksQuery.ToListAsync();
            return View(bookmarks);
        }

        // 2. CREARE (New) - Formularul
        [HttpGet]
        [Authorize(Roles = "Admin,User")]
        public IActionResult New()
        {
            ViewBag.Categories = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(_context.Categories, "Id", "Name");
            return View(new Bookmark());
        }

        // 2. CREARE (New) - POST
        [HttpPost]
        [Authorize(Roles = "Admin,User")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> New(Bookmark bookmark)
        {
            var userId = _userManager.GetUserId(User);
            bookmark.UserId = userId;
            bookmark.CreatedAt = DateTime.Now;

            // --- LOGICA PENTRU TAG-URI (Identica cu originalul) ---
            if (!string.IsNullOrEmpty(bookmark.TagString))
            {
                var tagNames = bookmark.TagString.Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var tagName in tagNames)
                {
                    var cleanName = tagName.Trim();
                    if (string.IsNullOrEmpty(cleanName)) continue;

                    var existingTag = await _context.Tags.FirstOrDefaultAsync(t => t.Name == cleanName);
                    if (existingTag != null) bookmark.Tags.Add(existingTag);
                    else bookmark.Tags.Add(new Tag { Name = cleanName });
                }
            }

            ModelState.Remove("UserId");
            ModelState.Remove("Category");
            ModelState.Remove("User");
            ModelState.Remove("Tags");

            if (ModelState.IsValid)
            {
                _context.Add(bookmark);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Categories = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(_context.Categories, "Id", "Name");
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
                                         .FirstOrDefaultAsync(b => b.Id == id);

            if (bookmark == null) return NotFound();

            var currentUserId = _userManager.GetUserId(User);

            // Doar proprietarul poate edita (chiar daca esti Admin, nu modifici continutul altuia)
            if (bookmark.UserId != currentUserId)
            {
                // Redirectionam catre Index daca nu e proprietar (securitate)
                return RedirectToAction(nameof(Index));
            }

            var tagNames = bookmark.Tags.Select(t => t.Name);
            bookmark.TagString = string.Join(", ", tagNames);

            ViewBag.Categories = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(_context.Categories, "Id", "Name", bookmark.CategoryId);
            return View(bookmark);
        }

        // 3. EDITARE (Edit) - POST
        [HttpPost]
        [Authorize(Roles = "Admin,User")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Bookmark bookmark)
        {
            if (id != bookmark.Id) return NotFound();

            var currentUserId = _userManager.GetUserId(User);

            // Incarcam bookmark-ul original din baza
            var existingBookmark = await _context.Bookmarks
                                                 .Include(b => b.Tags)
                                                 .FirstOrDefaultAsync(b => b.Id == id);

            // Verificare securitate: doar proprietarul editeaza
            if (existingBookmark == null || existingBookmark.UserId != currentUserId)
            {
                return Forbid(); // Sau RedirectToAction("Index")
            }

            // Actualizare campuri
            existingBookmark.Title = bookmark.Title;
            existingBookmark.Description = bookmark.Description;
            existingBookmark.MediaUrl = bookmark.MediaUrl;
            existingBookmark.CategoryId = bookmark.CategoryId;
            existingBookmark.IsPublic = bookmark.IsPublic; // Am adaugat si IsPublic, poate vrea sa schimbe vizibilitatea

            // --- ACTUALIZARE TAG-URI ---
            existingBookmark.Tags.Clear();
            if (!string.IsNullOrEmpty(bookmark.TagString))
            {
                var tagNames = bookmark.TagString.Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var tagName in tagNames)
                {
                    var cleanName = tagName.Trim();
                    if (string.IsNullOrEmpty(cleanName)) continue;

                    var tag = await _context.Tags.FirstOrDefaultAsync(t => t.Name == cleanName)
                              ?? new Tag { Name = cleanName };

                    existingBookmark.Tags.Add(tag);
                }
            }

            try
            {
                _context.Update(existingBookmark);
                await _context.SaveChangesAsync();
                await DeleteOrphanTags();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!BookmarkExists(bookmark.Id)) return NotFound();
                else throw;
            }
            return RedirectToAction(nameof(Index));
        }

        // 4. STERGERE (Delete) - GET (Confirmare)
        [HttpGet]
        [Authorize(Roles = "Admin,User")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var bookmark = await _context.Bookmarks
                                         .Include(b => b.User) // Includem User pentru a vedea cine l-a creat in view
                                         .FirstOrDefaultAsync(m => m.Id == id);

            if (bookmark == null) return NotFound();

            var currentUserId = _userManager.GetUserId(User);

            // Adminul SAU Proprietarul pot sterge [cite: 2]
            if (bookmark.UserId != currentUserId && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            return View(bookmark);
        }

        // 4. STERGERE (Delete) - POST (Executare)
        [HttpPost, ActionName("Delete")]
        [Authorize(Roles = "Admin,User")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var bookmark = await _context.Bookmarks.FindAsync(id);

            if (bookmark == null) return NotFound();

            var currentUserId = _userManager.GetUserId(User);

            // Verificare finala de securitate inainte de stergere efectiva
            if (bookmark.UserId != currentUserId && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            _context.Bookmarks.Remove(bookmark);
            await _context.SaveChangesAsync();
            await DeleteOrphanTags();
            return RedirectToAction(nameof(Index));
        }

        // 5. DETALII (Show)
        [HttpGet]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var bookmark = await _context.Bookmarks
                                         .Include(b => b.Tags)
                                         .Include(b => b.User)
                                         .FirstOrDefaultAsync(m => m.Id == id);

            if (bookmark == null) return NotFound();

            var currentUserId = _userManager.GetUserId(User);

            if (bookmark.UserId != currentUserId && !bookmark.IsPublic && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            return View(bookmark);
        }

        // Metodă ajutătoare pentru ștergerea tag-urilor orfane
        private async Task DeleteOrphanTags()
        {
            // Căutăm tag-urile care nu mai au niciun bookmark asociat
            var orphanTags = await _context.Tags
                                   .Where(t => !t.Bookmarks.Any())
                                   .ToListAsync();

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