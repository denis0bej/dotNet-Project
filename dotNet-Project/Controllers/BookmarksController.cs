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
using dotNet_Project.Services;

namespace dotNet_Project.Controllers
{
    [Authorize]
    public class BookmarksController : Controller
    {
        private readonly OpenAiService _aiService;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<BookmarksController> _logger;
        private readonly IWebHostEnvironment _env;

        public BookmarksController(ApplicationDbContext context,
                                   UserManager<ApplicationUser> userManager,
                                   ILogger<BookmarksController> logger,
                                   IWebHostEnvironment env,
                                   OpenAiService aiService)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _env = env;
            _aiService = aiService;
        }

        // 1. LISTARE (Index) - Include Căutare, Sortare și Paginare
        public async Task<IActionResult> Index(string searchString, string sortOrder, int? pageNumber)
        {
            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentSort"] = sortOrder;

            int pageSize = 15;
            int pageIndex = pageNumber ?? 1;

            var currentUserId = _userManager.GetUserId(User);
            var isAdmin = User.IsInRole("Admin");

            IQueryable<Bookmark> bookmarksQuery = _context.Bookmarks
                                                          .Include(b => b.Tags)
                                                          .Include(b => b.Categories)
                                                          .Include(b => b.Votes)
                                                          .Include(b => b.User);

            // Filtrare strictă pe User (My Collection) - doar Admin vede tot
            if (!isAdmin)
            {
                bookmarksQuery = bookmarksQuery.Where(b => b.UserId == currentUserId);
            }

            // LOGICA DE CĂUTARE AVANSATĂ
            if (!string.IsNullOrEmpty(searchString))
            {
                bookmarksQuery = bookmarksQuery.Where(b =>
                    b.Title.Contains(searchString) ||
                    (b.Description != null && b.Description.Contains(searchString)) ||
                    b.Categories.Any(c => c.Name.Contains(searchString)) ||
                    b.Tags.Any(t => t.Name.Contains(searchString)));

                // Prioritizare (Ranking: Titlu > Restul)
                bookmarksQuery = bookmarksQuery.OrderBy(b => b.Title.Contains(searchString) ? 0 : 1);

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
                // Sortare Standard
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

            // Paginare
            var totalItems = await bookmarksQuery.CountAsync();
            var bookmarksList = await bookmarksQuery
                                        .Skip((pageIndex - 1) * pageSize)
                                        .Take(pageSize)
                                        .ToListAsync();

            // Mapare ViewModel
            var viewModelList = bookmarksList.Select(b =>
            {
                var appUser = b.User as ApplicationUser;
                return new BookmarkCardViewModel
                {
                    Id = b.Id,
                    Title = b.Title,
                    Description = b.Description,
                    MediaUrl = b.MediaUrl,
                    LikeCount = b.Votes.Count,
                    IsLikedByCurrentUser = b.Votes.Any(v => v.UserId == currentUserId),
                    CategoryNames = b.Categories.Select(c => c.Name).ToList(),
                    Tags = b.Tags.Select(t => t.Name).ToList(),
                    IsPublic = b.IsPublic,
                    UserDisplayName = !string.IsNullOrEmpty(appUser?.FirstName) ? appUser.FirstName : (b.User?.UserName ?? "Unknown"),
                    UserProfilePicture = appUser?.ProfilePicture,
                    UserName = b.User?.UserName,
                    CreatedAt = b.CreatedAt
                };
            }).ToList();

            var paginatedResult = new PaginatedList<BookmarkCardViewModel>(viewModelList, totalItems, pageIndex, pageSize);

            return View(paginatedResult);
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

        // POST: Bookmarks/New
        [HttpPost]
        [Authorize(Roles = "Admin,User")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> New(Bookmark bookmark, int[] selectedCategories, string? newCategoriesString)
        {
            var userId = _userManager.GetUserId(User);
            bookmark.UserId = userId;
            bookmark.CreatedAt = DateTime.Now;

            // --- A. UPLOAD IMAGINE ---
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

            // --- B. CATEGORII EXISTENTE ---
            if (selectedCategories != null)
            {
                foreach (var catId in selectedCategories)
                {
                    var category = await _context.Categories.FindAsync(catId);
                    if (category != null) bookmark.Categories.Add(category);
                }
            }

            // --- C. CATEGORII NOI ---
            if (!string.IsNullOrEmpty(newCategoriesString))
            {
                var newCatNames = newCategoriesString.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                                     .Select(s => s.Trim())
                                                     .Distinct()
                                                     .ToList();

                foreach (var name in newCatNames)
                {
                    var existingCat = await _context.Categories
                        .FirstOrDefaultAsync(c => c.UserId == userId && c.Name.ToLower() == name.ToLower());

                    if (existingCat != null)
                    {
                        if (!bookmark.Categories.Contains(existingCat))
                        {
                            bookmark.Categories.Add(existingCat);
                        }
                    }
                    else
                    {
                        var newCat = new Category
                        {
                            Name = name,
                            UserId = userId,
                            IsPublic = true
                        };
                        _context.Categories.Add(newCat);
                        bookmark.Categories.Add(newCat);
                    }
                }
            }

            // --- D. TAG-URI ---
            if (!string.IsNullOrEmpty(bookmark.TagString))
            {
                var tagNames = bookmark.TagString.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                                 .Select(t => t.Trim())
                                                 .Distinct();

                foreach (var tagName in tagNames)
                {
                    var existingTag = await _context.Tags.FirstOrDefaultAsync(t => t.Name == tagName);
                    if (existingTag == null)
                    {
                        existingTag = new Tag { Name = tagName };
                        _context.Tags.Add(existingTag);
                    }
                    bookmark.Tags.Add(existingTag);
                }
            }

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

            var userCategories = _context.Categories.Where(c => c.UserId == userId);
            ViewBag.CategoryList = userCategories.Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.Name
            }).ToList();

            return View(bookmark);
        }

        // 3. EDITARE (Edit) - Formularul
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

            // SECURIZARE: Doar proprietarul poate vedea formularul de editare.
            // Am scos !User.IsInRole("Admin") pentru ca Adminul sa NU poata edita.
            if (bookmark.UserId != currentUserId)
            {
                return Forbid();
            }

            bookmark.TagString = string.Join(", ", bookmark.Tags.Select(t => t.Name));

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

        // 3. EDITARE (Edit) - Salvare
        [HttpPost]
        [Authorize(Roles = "Admin,User")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Bookmark bookmark, int[] selectedCategories, string? newCategoriesString)
        {
            if (id != bookmark.Id) return NotFound();
            var currentUserId = _userManager.GetUserId(User);

            var existingBookmark = await _context.Bookmarks
                                                 .Include(b => b.Categories)
                                                 .Include(b => b.Tags)
                                                 .FirstOrDefaultAsync(b => b.Id == id);

            if (existingBookmark == null) return NotFound();

            // SECURIZARE: Doar proprietarul poate salva modificari.
            // Am scos !User.IsInRole("Admin") pentru ca Adminul sa NU poata edita.
            if (existingBookmark.UserId != currentUserId)
            {
                return Forbid();
            }

            existingBookmark.Title = bookmark.Title;
            existingBookmark.Description = bookmark.Description;
            existingBookmark.IsPublic = bookmark.IsPublic;

            // Update Imagine
            if (bookmark.ImageUpload != null)
            {
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

            // Update CATEGORII
            existingBookmark.Categories.Clear();

            if (selectedCategories != null)
            {
                foreach (var catId in selectedCategories)
                {
                    var category = await _context.Categories.FindAsync(catId);
                    if (category != null) existingBookmark.Categories.Add(category);
                }
            }

            if (!string.IsNullOrEmpty(newCategoriesString))
            {
                var newCatNames = newCategoriesString.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                                     .Select(s => s.Trim())
                                                     .Distinct();

                foreach (var name in newCatNames)
                {
                    var existingCat = await _context.Categories
                        .FirstOrDefaultAsync(c => c.UserId == currentUserId && c.Name.ToLower() == name.ToLower());

                    if (existingCat != null)
                    {
                        if (!existingBookmark.Categories.Any(c => c.Id == existingCat.Id))
                            existingBookmark.Categories.Add(existingCat);
                    }
                    else
                    {
                        var newCat = new Category { Name = name, UserId = currentUserId, IsPublic = true };
                        _context.Categories.Add(newCat);
                        existingBookmark.Categories.Add(newCat);
                    }
                }
            }

            // Update TAG-URI
            existingBookmark.Tags.Clear();

            if (!string.IsNullOrEmpty(bookmark.TagString))
            {
                var tagNames = bookmark.TagString.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                                 .Select(t => t.Trim())
                                                 .Distinct();

                foreach (var tagName in tagNames)
                {
                    var existingTag = await _context.Tags.FirstOrDefaultAsync(t => t.Name == tagName);
                    if (existingTag == null)
                    {
                        existingTag = new Tag { Name = tagName };
                        _context.Tags.Add(existingTag);
                    }
                    existingBookmark.Tags.Add(existingTag);
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

        // 4. STERGERE (GET) - Admin allowed
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

            // Adminul POATE sterge, deci pastram verificarea !User.IsInRole("Admin")
            if (bookmark.UserId != currentUserId && !User.IsInRole("Admin")) return Forbid();

            return View(bookmark);
        }

        // 4. STERGERE (POST) - Admin allowed
        [HttpPost, ActionName("Delete")]
        [Authorize(Roles = "Admin,User")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var bookmark = await _context.Bookmarks.FindAsync(id);
            if (bookmark == null) return NotFound();
            var currentUserId = _userManager.GetUserId(User);

            // Adminul POATE sterge
            if (bookmark.UserId != currentUserId && !User.IsInRole("Admin")) return Forbid();

            DeleteLocalImage(bookmark.MediaUrl);

            _context.Bookmarks.Remove(bookmark);
            await _context.SaveChangesAsync();
            await DeleteOrphanTags();

            return RedirectToAction(nameof(Index));
        }

        // STERGERE AJAX (Doar Admin)
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAjax(int id)
        {
            var bookmark = await _context.Bookmarks.FindAsync(id);
            if (bookmark == null) return Json(new { success = false, message = "Postarea nu a fost găsită." });

            try
            {
                DeleteLocalImage(bookmark.MediaUrl);
                _context.Bookmarks.Remove(bookmark);
                await _context.SaveChangesAsync();
                await DeleteOrphanTags();
                return Json(new { success = true, message = "Postarea a fost ștearsă cu succes!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Eroare la ștergere: " + ex.Message });
            }
        }

        // 5. SHOW
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

        // Save To Collection
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

        // Toggle Like
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

        // AJAX Suggest Metadata
        [HttpPost]
        public async Task<IActionResult> SuggestMetadata([FromBody] MetadataRequest request)
        {
            if (string.IsNullOrEmpty(request.Title) && string.IsNullOrEmpty(request.Description))
            {
                return BadRequest("Titlul sau descrierea sunt necesare.");
            }

            var currentUserId = _userManager.GetUserId(User);
            var userCategories = await _context.Categories
                                               .Where(c => c.UserId == currentUserId)
                                               .Select(c => new { c.Id, c.Name })
                                               .ToListAsync();

            var existingNames = userCategories.Select(c => c.Name).ToList();
            var suggestion = await _aiService.GenerateMetadata(request.Title ?? "", request.Description ?? "", existingNames);

            if (suggestion == null) return StatusCode(500, "AI Service unavailable");

            var foundCategoryIds = new List<int>();
            var newSuggestedCategories = new List<string>();

            if (suggestion.Categories != null)
            {
                foreach (var aiCatName in suggestion.Categories)
                {
                    var match = userCategories.FirstOrDefault(c => string.Equals(c.Name, aiCatName, StringComparison.OrdinalIgnoreCase));
                    if (match != null)
                        foundCategoryIds.Add(match.Id);
                    else
                        newSuggestedCategories.Add(aiCatName);
                }
            }

            string tagsCommaSeparated = "";
            if (suggestion.Tags != null && suggestion.Tags.Any())
            {
                tagsCommaSeparated = string.Join(", ", suggestion.Tags);
            }

            return Json(new
            {
                tags = tagsCommaSeparated,
                categoryIds = foundCategoryIds,
                newCategories = newSuggestedCategories
            });
        }

        // Helpers
        private async Task DeleteOrphanTags()
        {
            var orphanTags = await _context.Tags.Where(t => !t.Bookmarks.Any()).ToListAsync();
            if (orphanTags.Any())
            {
                _context.Tags.RemoveRange(orphanTags);
                await _context.SaveChangesAsync();
            }
        }

        private bool BookmarkExists(int id) => _context.Bookmarks.Any(e => e.Id == id);

        private void DeleteLocalImage(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return;
            if (relativePath.StartsWith("/images/"))
            {
                var absolutePath = Path.Combine(_env.WebRootPath, relativePath.TrimStart('/'));
                if (System.IO.File.Exists(absolutePath)) try { System.IO.File.Delete(absolutePath); } catch { }
            }
        }

        public class MetadataRequest
        {
            public string Title { get; set; }
            public string Description { get; set; }
        }
    }
}