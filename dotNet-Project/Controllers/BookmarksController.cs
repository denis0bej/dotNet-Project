using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using dotNet_Project.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering; // Necesar pentru SelectListItem
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
                                                          .Include(b => b.Tags)
                                                          .Include(b => b.Categories); // ACUM INCLUDEM LISTA DE CATEGORII

            if (isAdmin)
            {
                // Adminul vede TOT
            }
            else
            {
                // Userul vede: Ale sale SAU Cele Publice
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
            var currentUserId = _userManager.GetUserId(User);

            // Selectam categoriile userului curent pentru a popula checkbox-urile
            var userCategories = _context.Categories.Where(c => c.UserId == currentUserId);

            // Trimitem lista sub forma de SelectListItem pentru a fi usor de iterat in View
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
        // Adaugam parametrul selectedCategories[] care vine din checkbox-urile din View
        public async Task<IActionResult> New(Bookmark bookmark, int[] selectedCategories)
        {
            var userId = _userManager.GetUserId(User);
            bookmark.UserId = userId;
            bookmark.CreatedAt = DateTime.Now;

            // --- LOGICA PENTRU CATEGORII (NOU) ---
            if (selectedCategories != null)
            {
                foreach (var catId in selectedCategories)
                {
                    var category = await _context.Categories.FindAsync(catId);
                    if (category != null)
                    {
                        bookmark.Categories.Add(category);
                    }
                }
            }

            // --- LOGICA PENTRU TAG-URI (Veche) ---
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

            // Stergem validarile care nu mai sunt necesare sau sunt gestionate manual
            ModelState.Remove("UserId");
            ModelState.Remove("Categories"); // Nu validam automat colectia
            ModelState.Remove("User");
            ModelState.Remove("Tags");

            if (ModelState.IsValid)
            {
                _context.Add(bookmark);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            // Daca e eroare, reincarcam lista de categorii ca sa nu dispara checkbox-urile
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
                                         .Include(b => b.Categories) // Includem categoriile existente
                                         .FirstOrDefaultAsync(b => b.Id == id);

            if (bookmark == null) return NotFound();

            var currentUserId = _userManager.GetUserId(User);

            if (bookmark.UserId != currentUserId)
            {
                return RedirectToAction(nameof(Index));
            }

            // Tag-uri
            var tagNames = bookmark.Tags.Select(t => t.Name);
            bookmark.TagString = string.Join(", ", tagNames);

            // --- CATEGORII ---
            // Luam toate categoriile userului
            var allUserCategories = _context.Categories.Where(c => c.UserId == currentUserId);

            // Luam ID-urile categoriilor deja selectate
            var selectedIds = bookmark.Categories.Select(c => c.Id).ToList();

            // Construim lista cu proprietatea 'Selected' setata corect
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

            // Incarcam bookmark-ul original + Categorii + Tags
            var existingBookmark = await _context.Bookmarks
                                                 .Include(b => b.Tags)
                                                 .Include(b => b.Categories)
                                                 .FirstOrDefaultAsync(b => b.Id == id);

            if (existingBookmark == null || existingBookmark.UserId != currentUserId)
            {
                return Forbid();
            }

            // Actualizare campuri simple
            existingBookmark.Title = bookmark.Title;
            existingBookmark.Description = bookmark.Description;
            existingBookmark.MediaUrl = bookmark.MediaUrl;
            existingBookmark.IsPublic = bookmark.IsPublic;

            // --- ACTUALIZARE CATEGORII ---
            // 1. Golim lista curenta
            existingBookmark.Categories.Clear();

            // 2. Adaugam noile selectii din checkbox-uri
            if (selectedCategories != null)
            {
                foreach (var catId in selectedCategories)
                {
                    var category = await _context.Categories.FindAsync(catId);
                    if (category != null) existingBookmark.Categories.Add(category);
                }
            }

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

        // 4. STERGERE (Delete) - GET
        [HttpGet]
        [Authorize(Roles = "Admin,User")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var bookmark = await _context.Bookmarks
                                         .Include(b => b.User)
                                         .Include(b => b.Categories) // Optional, daca vrei sa le afisezi la stergere
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
        [Authorize(Roles = "Admin,User")]
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
                                         .Include(b => b.Categories) // Includem si categoriile
                                         .FirstOrDefaultAsync(m => m.Id == id);

            if (bookmark == null) return NotFound();

            var currentUserId = _userManager.GetUserId(User);

            if (bookmark.UserId != currentUserId && !bookmark.IsPublic && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            return View(bookmark);
        }

        // GET: Bookmarks/SaveToCollection/5
        [Authorize]
        public async Task<IActionResult> SaveToCollection(int? id)
        {
            if (id == null) return NotFound();

            // 1. Luam bookmark-ul original (doar pentru a afisa titlul userului)
            var bookmark = await _context.Bookmarks.FindAsync(id);
            if (bookmark == null) return NotFound();

            var currentUserId = _userManager.GetUserId(User);

            // 2. Selectam categoriile MELE
            var myCategories = _context.Categories
                                       .Where(c => c.UserId == currentUserId)
                                       .Include(c => c.Bookmarks); // Includem bookmarkurile existente ca sa vedem unde e deja salvat

            // 3. Generam lista de checkbox-uri
            // Verificam daca bookmark-ul e deja in vreuna din categoriile mele
            var categoriesList = await myCategories.ToListAsync();

            ViewBag.CategoryList = categoriesList.Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.Name,
                // Daca categoria contine deja acest bookmark, il bifam (checked)
                Selected = c.Bookmarks.Any(b => b.Id == bookmark.Id)
            }).ToList();

            return View(bookmark);
        }

        // POST: Bookmarks/SaveToCollection/5
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveToCollection(int id, int[] selectedCategories)
        {
            var bookmark = await _context.Bookmarks.FindAsync(id);
            if (bookmark == null) return NotFound();

            var currentUserId = _userManager.GetUserId(User);

            // Iteram prin categoriile selectate de user
            if (selectedCategories != null)
            {
                // 1. Luam toate categoriile userului curent (pentru securitate, sa nu salveze in categoria altuia)
                var myCategories = await _context.Categories
                                                 .Include(c => c.Bookmarks)
                                                 .Where(c => c.UserId == currentUserId)
                                                 .ToListAsync();

                foreach (var category in myCategories)
                {
                    // Verificam daca userul a bifat aceasta categorie
                    if (selectedCategories.Contains(category.Id))
                    {
                        // DACA BIFAT: Adaugam bookmark-ul in categorie (daca nu e deja acolo)
                        if (!category.Bookmarks.Any(b => b.Id == id))
                        {
                            category.Bookmarks.Add(bookmark);
                        }
                    }
                    else
                    {
                        // DACA NE-BIFAT: Il scoatem (Unsave) daca era acolo
                        // Asta permite userului sa si stearga bookmark-ul din colectiile lui de aici
                        var existingLink = category.Bookmarks.FirstOrDefault(b => b.Id == id);
                        if (existingLink != null)
                        {
                            category.Bookmarks.Remove(existingLink);
                        }
                    }
                }

                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Index");
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
        private bool BookmarkExists(int id)
        {
            return _context.Bookmarks.Any(e => e.Id == id);
        }
    }
}