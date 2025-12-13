using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using dotNet_Project.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using dotNet_Project.Models;

namespace dotNet_Project.Controllers
{
    // [Authorize] forțează utilizatorul să fie logat pentru a accesa oricare dintre aceste metode
    [Authorize]
    public class BookmarksController : Controller
    {
        private readonly ApplicationDbContext _context;
        // NOTA: Daca folosesti IdentityUser standard si nu ai clasa ApplicationUser, schimba mai jos in <IdentityUser>
        private readonly UserManager<ApplicationUser> _userManager;

        public BookmarksController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // 1. LISTARE (Index) - Afișează doar bookmark-urile utilizatorului curent
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);

            // Luăm doar bookmark-urile create de utilizatorul logat
            var myBookmarksQuery = _context.Bookmarks
                                           .Where(b => b.UserId == userId);

            // NOTA: Decomenteaza linia de mai jos doar daca ai implementat relatia cu Category in modelul Bookmark
            // myBookmarksQuery = myBookmarksQuery.Include(b => b.Category);

            var myBookmarks = await myBookmarksQuery.ToListAsync();
            return View(myBookmarks);
        }

        // 2. CREARE (Create) - Formularul
        [HttpGet]
        public IActionResult New()
        {
            // Trimitem lista de categorii către View pentru Dropdown
            ViewBag.Categories = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(_context.Categories, "Id", "Name");
            return View();
        }

        // 2. CREARE (Create) - Procesarea datelor trimise (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> New(Bookmark bookmark)
        {

            var userId = _userManager.GetUserId(User);
            bookmark.UserId = userId;
            bookmark.CreatedAt = DateTime.Now; // Atentie: Asigura-te ca proprietatea din Model se numeste CreatedAt sau DateCreated

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

            var bookmark = await _context.Bookmarks.FindAsync(id);

            if (bookmark == null) return NotFound();

            // VERIFICARE CRITICĂ: Utilizatorul are voie să editeze DOAR propriile bookmark-uri 
            var currentUserId = _userManager.GetUserId(User);
            if (bookmark.UserId != currentUserId)
            {
                return Forbid();
            }

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

            // Luam entitatea originala fara tracking pentru a verifica proprietarul
            var existingBookmark = await _context.Bookmarks.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id);

            if (existingBookmark == null || existingBookmark.UserId != currentUserId)
            {
                return Forbid();
            }

            // Fortam UserId-ul sa ramana cel corect, chiar daca vine altceva din formular
            bookmark.UserId = currentUserId;

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(bookmark);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!BookmarkExists(bookmark.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Categories = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(_context.Categories, "Id", "Name", bookmark.CategoryId);
            return View(bookmark);
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
    }
}