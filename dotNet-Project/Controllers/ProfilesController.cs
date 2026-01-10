using dotNet_Project.Data;
using dotNet_Project.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace dotNet_Project.Controllers
{
    // Scoatem [Authorize] de la nivelul clasei pentru ca profilul sa poata fi public
    public class ProfilesController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ApplicationDbContext _context;

        public ProfilesController(
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment webHostEnvironment,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _webHostEnvironment = webHostEnvironment;
            _context = context;
        }

        // GET: /Profiles/Show/5
        public async Task<IActionResult> Show(string id, int? categoryPage, int? bookmarkPage)
        {
            var currentUserId = _userManager.GetUserId(User);
            string targetUserId = id ?? currentUserId;
            int pageSize = 15;

            if (targetUserId == null) return Redirect("/Identity/Account/Login");

            var targetUser = await _userManager.FindByIdAsync(targetUserId);
            if (targetUser == null) return NotFound("User not found");

            // --- 1. PREGĂTIRE QUERY-URI ---
            var categoriesQuery = _context.Categories.Where(c => c.UserId == targetUserId).AsQueryable();

            var bookmarksQuery = _context.Bookmarks
                                        .Include(b => b.Categories)
                                        .Include(b => b.Votes)
                                        .Include(b => b.Tags) // <--- AM ADĂUGAT TAGS AICI
                                        .Where(b => b.UserId == targetUserId)
                                        .AsQueryable();

            // Reguli vizibilitate
            bool isOwner = (currentUserId == targetUserId);
            bool isAdmin = User.IsInRole("Admin");

            if (!isOwner && !isAdmin)
            {
                categoriesQuery = categoriesQuery.Where(c => c.IsPublic == true);
                bookmarksQuery = bookmarksQuery.Where(b => b.IsPublic == true);
            }

            // Sortare implicită
            categoriesQuery = categoriesQuery.OrderByDescending(c => c.Name);
            bookmarksQuery = bookmarksQuery.OrderByDescending(b => b.CreatedAt);

            // --- 2. PAGINARE CATEGORII ---
            int catPageIndex = categoryPage ?? 1;
            var totalCategories = await categoriesQuery.CountAsync();

            var pagedCategories = await categoriesQuery
                .Skip((catPageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var paginatedCategories = new PaginatedList<Category>(
                pagedCategories, totalCategories, catPageIndex, pageSize);


            // --- 3. PAGINARE BOOKMARK-URI ---
            int bookPageIndex = bookmarkPage ?? 1;
            var totalBookmarks = await bookmarksQuery.CountAsync();

            var pagedBookmarksRaw = await bookmarksQuery
                .Skip((bookPageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Mapare către ViewModel
            var bookmarkViewModels = pagedBookmarksRaw.Select(b => new BookmarkCardViewModel
            {
                Id = b.Id,
                Title = b.Title,
                MediaUrl = b.MediaUrl,
                LikeCount = b.Votes.Count,
                IsPublic = b.IsPublic,
                CategoryNames = b.Categories.Select(c => c.Name).ToList(),
                Tags = b.Tags.Select(t => t.Name).ToList(), // <--- AM MAPAT TAGS AICI
                IsLikedByCurrentUser = currentUserId != null && b.Votes.Any(v => v.UserId == currentUserId),
                UserDisplayName = $"{targetUser.FirstName} {targetUser.LastName}".Trim(),
                UserProfilePicture = targetUser.ProfilePicture,
                UserName = targetUser.UserName
            }).ToList();

            var paginatedBookmarks = new PaginatedList<BookmarkCardViewModel>(
                bookmarkViewModels, totalBookmarks, bookPageIndex, pageSize);


            // --- 4. CONSTRUIRE MODEL ---
            var model = new UserProfileViewModel
            {
                UserId = targetUser.Id,
                Email = targetUser.Email,
                FullName = !string.IsNullOrEmpty($"{targetUser.FirstName} {targetUser.LastName}".Trim())
                           ? $"{targetUser.FirstName} {targetUser.LastName}".Trim()
                           : targetUser.UserName,
                Description = targetUser.Description,
                ProfilePicture = targetUser.ProfilePicture,
                IsOwner = isOwner,
                Categories = paginatedCategories,
                Bookmarks = paginatedBookmarks
            };

            // Stocăm pagina curentă în ViewBag pentru tab activ
            ViewBag.ActiveTab = (bookmarkPage > 1 && categoryPage == null) ? "bookmarks" : "collections";

            return View(model);
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Edit()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var model = new ProfileDetailsModel
            {
                FirstName = user.FirstName,
                LastName = user.LastName,
                Description = user.Description,
                CurrentProfilePicture = user.ProfilePicture
            };
            return View(model);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ProfileDetailsModel model)
        {

            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            user.FirstName = model.FirstName;
            user.LastName = model.LastName;
            user.Description = model.Description;

            if (model.ProfileImage != null)
            {
                DeleteLocalImage(user.ProfilePicture);

                string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "profiles");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
                string uniqueFileName = Guid.NewGuid().ToString() + "_" + model.ProfileImage.FileName;
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await model.ProfileImage.CopyToAsync(fileStream);
                }
                user.ProfilePicture = "/images/profiles/" + uniqueFileName;
            }

            await _userManager.UpdateAsync(user);
            TempData["SuccessMessage"] = "Profile updated successfully!";
            return RedirectToAction("Edit");
        }

        private void DeleteLocalImage(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return;
            if (relativePath.StartsWith("/images/"))
            {
                var absolutePath = Path.Combine(_webHostEnvironment.WebRootPath, relativePath.TrimStart('/'));
                if (System.IO.File.Exists(absolutePath))
                {
                    System.IO.File.Delete(absolutePath);
                }
            }
        }
    }
}