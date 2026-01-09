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
    [Authorize]
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
            var userId = _userManager.GetUserId(User);

            var myBookmarksQuery = _context.Bookmarks
                        .Include(b => b.Tags)
                        .Include(b => b.Votes) // <--- AM ADAUGAT ASTA
                        .Where(b => b.UserId == userId);

            var myBookmarks = await myBookmarksQuery.ToListAsync();
            return View(myBookmarks);
        }

        // 2. CREARE (New) - Formularul
        [HttpGet]
        public IActionResult New()
        {
            ViewBag.Categories = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(_context.Categories, "Id", "Name");
            return View();
        }

        // 2. CREARE (New) - Procesarea datelor (MODIFICAT)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> New(Bookmark bookmark)
        {
            var userId = _userManager.GetUserId(User);
            bookmark.UserId = userId;
            bookmark.CreatedAt = DateTime.Now;

            // --- MODIFICARE: AM SCOS LOGICA DE TAG-URI MANUALE ---
            // Aici nu mai procesăm TagString. Tag-urile vor fi adăugate ulterior de AI.

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

        // 3. EDITARE (Edit) - Formularul (MODIFICAT)
        [HttpGet]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var bookmark = await _context.Bookmarks
                                 .Include(b => b.Tags)
                                 .FirstOrDefaultAsync(b => b.Id == id);

            if (bookmark == null) return NotFound();

            var currentUserId = _userManager.GetUserId(User);
            if (bookmark.UserId != currentUserId)
            {
                return Forbid();
            }

            // --- MODIFICARE: AM SCOS POPULAREA INPUTULUI DE TAG-URI ---
            // Nu mai trimitem tag-urile existente în string, deoarece utilizatorul nu le mai editează manual.

            ViewBag.Categories = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(_context.Categories, "Id", "Name", bookmark.CategoryId);
            return View(bookmark);
        }

        // 3. EDITARE (Edit) - Salvarea modificărilor (MODIFICAT)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Bookmark bookmark)
        {
            if (id != bookmark.Id) return NotFound();

            var currentUserId = _userManager.GetUserId(User);

            var existingBookmark = await _context.Bookmarks
                                         .Include(b => b.Tags)
                                         .FirstOrDefaultAsync(b => b.Id == id);

            if (existingBookmark == null || existingBookmark.UserId != currentUserId)
            {
                return Forbid();
            }

            // Actualizăm datele simple
            existingBookmark.Title = bookmark.Title;
            existingBookmark.Description = bookmark.Description;
            existingBookmark.MediaUrl = bookmark.MediaUrl;
            existingBookmark.CategoryId = bookmark.CategoryId;

            // --- MODIFICARE: AM SCOS ACTUALIZAREA TAG-URILOR ---
            // Tag-urile existente rămân neschimbate la editarea manuală a utilizatorului.

            try
            {
                _context.Update(existingBookmark);
                await _context.SaveChangesAsync();

                // Putem lăsa curățarea tag-urilor orfane, deși aici nu se vor produce orfani noi
                await DeleteOrphanTags();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!BookmarkExists(bookmark.Id)) return NotFound();
                else throw;
            }
            return RedirectToAction(nameof(Index));
        }

        // 4. ȘTERGERE (Delete)
        [HttpGet]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var bookmarkQuery = _context.Bookmarks.AsQueryable();
            // bookmarkQuery = bookmarkQuery.Include(b => b.Category);

            var bookmark = await bookmarkQuery.FirstOrDefaultAsync(m => m.Id == id);

            if (bookmark == null) return NotFound();

            var currentUserId = _userManager.GetUserId(User);
            if (bookmark.UserId != currentUserId)
            {
                return Forbid();
            }

            return View(bookmark);
        }

        // 4. ȘTERGERE (Delete) - Confirmat
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var bookmark = await _context.Bookmarks.FindAsync(id);

            var currentUserId = _userManager.GetUserId(User);
            if (bookmark == null || bookmark.UserId != currentUserId)
            {
                return Forbid();
            }

            _context.Bookmarks.Remove(bookmark);
            await _context.SaveChangesAsync();

            // Aici e util să păstrăm ștergerea tag-urilor orfane, 
            // în caz că un tag era legat DOAR de bookmark-ul șters.
            await DeleteOrphanTags();

            return RedirectToAction(nameof(Index));
        }

        private bool BookmarkExists(int id)
        {
            return _context.Bookmarks.Any(e => e.Id == id);
        }

        // 5. DETALII
        [HttpGet]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var bookmark = await _context.Bookmarks
                .Include(b => b.Tags)
                .Include(b => b.Votes) // <--- AM ADAUGAT ASTA
                .FirstOrDefaultAsync(m => m.Id == id);

            if (bookmark == null) return NotFound();

            return View(bookmark);
        }

        private async Task DeleteOrphanTags()
        {
            var orphanTags = await _context.Tags
                                   .Where(t => !t.Bookmarks.Any())
                                   .ToListAsync();

            if (orphanTags.Any())
            {
                _context.Tags.RemoveRange(orphanTags);
                await _context.SaveChangesAsync();
            }
        }

        // 6. VOTARE (AJAX) - Modificat să returneze JSON
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> ToggleLike([FromBody] int id) // Primim ID-ul din body-ul JSON
        {
            var bookmark = await _context.Bookmarks.FindAsync(id);
            if (bookmark == null)
            {
                return NotFound();
            }

            var currentUserId = _userManager.GetUserId(User);
            var existingVote = await _context.Votes
                                    .FirstOrDefaultAsync(v => v.BookmarkId == id && v.UserId == currentUserId);

            bool isLiked; // Starea finală (dacă e like sau nu)

            if (existingVote != null)
            {
                // UNLIKE
                _context.Votes.Remove(existingVote);
                if (bookmark.LikeCount > 0) bookmark.LikeCount--;
                isLiked = false;
            }
            else
            {
                // LIKE
                var newVote = new Vote { BookmarkId = id, UserId = currentUserId };
                _context.Votes.Add(newVote);
                bookmark.LikeCount++;
                isLiked = true;
            }

            await _context.SaveChangesAsync();

            // Returnăm JSON pentru JavaScript
            return Json(new { success = true, newCount = bookmark.LikeCount, isLiked = isLiked });
        }
    }
}