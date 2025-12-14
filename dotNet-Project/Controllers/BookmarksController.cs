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
    // [Authorize] forțează utilizatorul să fie logat pentru a accesa oricare dintre aceste metode
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

        // 1. LISTARE (Index) - Afișează doar bookmark-urile utilizatorului curent
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);

            // Luăm doar bookmark-urile create de utilizatorul logat
            var myBookmarksQuery = _context.Bookmarks
                       .Include(b => b.Tags) 
                       .Where(b => b.UserId == userId);

            // NOTA: Decomenteaza linia de mai jos doar daca ai implementat relatia cu Category in modelul Bookmark
            // myBookmarksQuery = myBookmarksQuery.Include(b => b.Category);

            var myBookmarks = await myBookmarksQuery.ToListAsync();
            return View(myBookmarks);
        }

        // 2. CREARE (New) - Formularul
        [HttpGet]
        public IActionResult New()
        {
            // Trimitem lista de categorii către View pentru Dropdown
            ViewBag.Categories = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(_context.Categories, "Id", "Name");
            return View();
        }

        // 2. CREARE (New) - Procesarea datelor trimise (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> New(Bookmark bookmark)
        {
            var userId = _userManager.GetUserId(User);
            bookmark.UserId = userId;
            bookmark.CreatedAt = DateTime.Now;

            // --- LOGICA PENTRU TAG-URI ---
            if (!string.IsNullOrEmpty(bookmark.TagString))
            {
                // 1. Spargem textul în cuvinte (ex: "soare, mare" -> ["soare", "mare"])
                var tagNames = bookmark.TagString.Split(',', StringSplitOptions.RemoveEmptyEntries);

                foreach (var tagName in tagNames)
                {
                    var cleanName = tagName.Trim();
                    if (string.IsNullOrEmpty(cleanName)) continue;

                    // 2. Căutăm dacă există deja tag-ul în baza de date
                    var existingTag = await _context.Tags
                                            .FirstOrDefaultAsync(t => t.Name == cleanName);

                    if (existingTag != null)
                    {
                        bookmark.Tags.Add(existingTag);
                    }
                    else
                    {
                        // 3. Dacă nu există, creăm unul nou
                        bookmark.Tags.Add(new Tag { Name = cleanName });
                    }
                }
            }

            ModelState.Remove("UserId");
            ModelState.Remove("Category");
            ModelState.Remove("User");
            ModelState.Remove("Tags"); // Nu validăm colecția, validăm doar string-ul dacă e cazul

            if (ModelState.IsValid)
            {
                _context.Add(bookmark);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Categories = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(_context.Categories, "Id", "Name");
            return View(bookmark);
        }

        // 3. EDITARE (Edit) - Formularul
        [HttpGet]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            // IMPORTANT: Includem Tags aici!
            var bookmark = await _context.Bookmarks
                                 .Include(b => b.Tags)
                                 .FirstOrDefaultAsync(b => b.Id == id);

            if (bookmark == null) return NotFound();

            var currentUserId = _userManager.GetUserId(User);
            if (bookmark.UserId != currentUserId)
            {
                return Forbid();
            }

            // --- POPULĂM INPUTUL DIN VIEW ---
            // Transformăm lista de obiecte Tag într-un string (ex: "Tag1, Tag2")
            var tagNames = bookmark.Tags.Select(t => t.Name);
            bookmark.TagString = string.Join(", ", tagNames);

            ViewBag.Categories = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(_context.Categories, "Id", "Name", bookmark.CategoryId);
            return View(bookmark);
        }

        // 3. EDITARE (Edit) - Salvarea modificărilor (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Bookmark bookmark)
        {
            if (id != bookmark.Id) return NotFound();

            var currentUserId = _userManager.GetUserId(User);

            // 1. Încărcăm bookmark-ul din bază CU TOT CU TAG-URI (pentru a le putea modifica)
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
            // Nu actualizăm UserId sau CreatedAt

            // --- ACTUALIZARE TAG-URI ---
            // Ștergem legăturile vechi (doar din memorie momentan)
            existingBookmark.Tags.Clear();

            if (!string.IsNullOrEmpty(bookmark.TagString))
            {
                var tagNames = bookmark.TagString.Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var tagName in tagNames)
                {
                    var cleanName = tagName.Trim();
                    if (string.IsNullOrEmpty(cleanName)) continue;

                    // Căutăm tag-ul existent sau creăm unul nou
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

        // 4. ȘTERGERE (Delete) - Confirmare
        [HttpGet]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var bookmarkQuery = _context.Bookmarks.AsQueryable();

            // NOTA: Decomenteaza linia de mai jos doar daca ai relatia cu Category
            // bookmarkQuery = bookmarkQuery.Include(b => b.Category);

            var bookmark = await bookmarkQuery.FirstOrDefaultAsync(m => m.Id == id);

            if (bookmark == null) return NotFound();

            // VERIFICARE CRITICĂ: Doar proprietarul poate șterge 
            var currentUserId = _userManager.GetUserId(User);
            if (bookmark.UserId != currentUserId)
            {
                return Forbid();
            }

            return View(bookmark);
        }

        // 4. ȘTERGERE (Delete) - Executare (POST)
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
            await DeleteOrphanTags();
            return RedirectToAction(nameof(Index));
        }

        private bool BookmarkExists(int id)
        {
            return _context.Bookmarks.Any(e => e.Id == id);
        }


        // 5. DETALII (Details/Show) - Afișarea unui singur bookmark
        [HttpGet]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var bookmarkQuery = _context.Bookmarks.AsQueryable();

            // NOTA: Decomentează linia de mai jos dacă vrei să afișezi numele categoriei
            // bookmarkQuery = bookmarkQuery.Include(b => b.Category);

            var bookmark = await bookmarkQuery.FirstOrDefaultAsync(m => m.Id == id);

            if (bookmark == null) return NotFound();

            // Verificăm dacă userul are voie să vadă (momentan lăsăm doar owner-ul, 
            // dar pe viitor aici vei permite și vizitatorilor să vadă bookmark-uri publice)
            // var currentUserId = _userManager.GetUserId(User);
            // if (bookmark.UserId != currentUserId) { return Forbid(); }

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

    }
}