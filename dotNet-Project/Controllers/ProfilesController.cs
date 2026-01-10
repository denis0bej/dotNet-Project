using dotNet_Project.Data; // Necesare pentru DbContext
using dotNet_Project.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace dotNet_Project.Controllers
{
    // Scoatem [Authorize] de la nivelul clasei pentru ca profilul sa poata fi public
    // Vom pune [Authorize] doar pe Edit
    public class ProfilesController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ApplicationDbContext _context; // Adaugam contextul DB

        public ProfilesController(
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment webHostEnvironment,
            ApplicationDbContext context) // Injectam in constructor
        {
            _userManager = userManager;
            _webHostEnvironment = webHostEnvironment;
            _context = context;
        }

        // GET: /Profiles/Show/5 (sau id-ul utilizatorului)
        public async Task<IActionResult> Show(string id)
        {
            var currentUserId = _userManager.GetUserId(User);

            // Daca nu primim niciun ID in URL, presupunem ca userul vrea sa isi vada propriul profil
            string targetUserId = id ?? currentUserId;

            // Daca targetUserId e null (nu e logat si nu a dat ID), redirect la login
            if (targetUserId == null)
            {
                return Redirect("/Identity/Account/Login");
            }

            // 1. Gasim utilizatorul tinta
            var targetUser = await _userManager.FindByIdAsync(targetUserId);
            if (targetUser == null) return NotFound("User not found");

            // 2. Gasim categoriile utilizatorului
            var categoriesQuery = _context.Categories.Where(c => c.UserId == targetUserId);

            // Regula de vizibilitate:
            // Daca eu NU sunt proprietarul profilului SI nu sunt Admin, vad doar categoriile publice
            bool isOwner = (currentUserId == targetUserId);
            bool isAdmin = User.IsInRole("Admin");

            if (!isOwner && !isAdmin)
            {
                categoriesQuery = categoriesQuery.Where(c => c.IsPublic == true);
            }

            var categories = await categoriesQuery.ToListAsync();

            // 3. Construim ViewModel-ul
            var model = new UserProfileViewModel
            {
                UserId = targetUser.Id,
                FullName = $"{targetUser.FirstName} {targetUser.LastName}".Trim(),
                Description = targetUser.Description,
                ProfilePicture = targetUser.ProfilePicture,
                IsOwner = isOwner,
                Categories = categories
            };

            if (string.IsNullOrEmpty(model.FullName)) model.FullName = targetUser.UserName;

            return View(model);
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Edit()
        {
            // ... codul tau existent pentru Edit GET ...
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
            // ... codul tau existent pentru Edit POST ...
            // (Copy-paste la ce ai scris in pasul anterior, e perfect)
            // Doar sa te asiguri ca metodele Edit raman in clasa.
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            user.FirstName = model.FirstName;
            user.LastName = model.LastName;
            user.Description = model.Description;

            if (model.ProfileImage != null)
            {
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
    }
}