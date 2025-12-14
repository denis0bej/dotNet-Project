using dotNet_Project.Data;
using dotNet_Project.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace dotNet_Project.Controllers
{
    public class CategoriesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public CategoriesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [Authorize(Roles = "Admin,User")]
        public async Task<IActionResult> Index()
        {
            var currentUserId = _userManager.GetUserId(User);

            // Adminul vede toate categoriile
            if (User.IsInRole("Admin"))
            {
                return View(await _context.Categories.Include(c => c.User).ToListAsync());
            }
            else
            {
                // Userul vede doar categoriile proprii
                return View(await _context.Categories
                                    .Where(c => c.UserId == currentUserId)
                                    .ToListAsync());
            }
        }

        // GET: Categories/New
        public IActionResult New()
        {
            return View();
        }

        // POST: Categories/New
        [HttpPost]
        [Authorize(Roles = "Admin,User")] // Trebuie sa fie autentificat
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> New([Bind("Id,Name,IsPublic")] Category category)
        {
            ModelState.Remove("UserId");
            ModelState.Remove("User");
            ModelState.Remove("Bookmarks");

            if (ModelState.IsValid)
            {
                category.UserId = _userManager.GetUserId(User);

                _context.Add(category);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(category);
        }

        // GET: Categories/Edit/5
        [Authorize(Roles = "Admin,User")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var category = await _context.Categories.FindAsync(id);
            if (category == null) return NotFound();

            var currentUserId = _userManager.GetUserId(User);
            // Daca nu e userul specific, nu are voie sa editeze (chiar daca e admin)
            if (category.UserId != currentUserId)
            {
                return RedirectToAction("Index");
            }

            return View(category);
        }

        // POST: Categories/Edit/5
        [HttpPost]
        [Authorize(Roles = "Admin,User")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,IsPublic")] Category category)
        {
            if (id != category.Id) return NotFound();

            // Preluam categoria originala din baza de date pentru a nu pierde UserId-ul
            var originalCategory = await _context.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);

            var currentUserId = _userManager.GetUserId(User);
            if (originalCategory.UserId != currentUserId)
            {
                return RedirectToAction("Index");
            }

            ModelState.Remove("UserId");
            ModelState.Remove("User");
            ModelState.Remove("Bookmarks");

            if (ModelState.IsValid)
            {
                try
                {
                    // Asiguram ca UserId ramane cel original
                    category.UserId = originalCategory.UserId;

                    _context.Update(category);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Categories.Any(e => e.Id == category.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(category);
        }

        // GET: Categories/Delete/5
        [Authorize(Roles = "Admin,User")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var category = await _context.Categories
                .Include(c => c.User)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (category == null) return NotFound();

            var currentUserId = _userManager.GetUserId(User);
            if (category.UserId != currentUserId && !User.IsInRole("Admin"))
            {
                RedirectToAction("AccessDenied");
            }

            return View(category);
        }

        // POST: Categories/Delete/5
        [HttpPost, ActionName("Delete")]
        [Authorize(Roles = "Admin,User")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var category = await _context.Categories.FindAsync(id);

            // Verificare securitate finala
            var currentUserId = _userManager.GetUserId(User);
            if (category.UserId != currentUserId && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            if (category != null)
            {
                _context.Categories.Remove(category);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

    }
}
