using dotNet_Project.Data;
using dotNet_Project.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace dotNet_Project.Controllers
{
    [Authorize]
    public class CommentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public CommentsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // POST: Adăugare Comentariu
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add([FromForm] Comment comment)
        {
            comment.CreatedAt = DateTime.Now;
            comment.UserId = _userManager.GetUserId(User);

            // --- FIX: Scoatem validarea pentru obiectele de navigare ---
            // Framework-ul se plânge că "User" și "Bookmark" sunt null, deși avem ID-urile lor.
            ModelState.Remove("User");
            ModelState.Remove("Bookmark");
            ModelState.Remove("UserId"); // Uneori e nevoie și pentru UserId dacă e setat manual

            if (ModelState.IsValid)
            {
                _context.Comments.Add(comment);
                await _context.SaveChangesAsync();
                return RedirectToAction("Show", "Bookmarks", new { id = comment.BookmarkId });
            }

            // (Opțional pentru debug) Dacă tot nu merge, poți pune un breakpoint aici să vezi erorile:
            // var errors = ModelState.Values.SelectMany(v => v.Errors);

            return RedirectToAction("Show", "Bookmarks", new { id = comment.BookmarkId });
        }

        // POST: Ștergere Comentariu
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Delete(int id)
        {
            var comment = await _context.Comments.FindAsync(id);

            if (comment == null)
            {
                return NotFound();
            }

            var currentUserId = _userManager.GetUserId(User);

            // Doar proprietarul comentariului sau Adminul pot șterge
            if (comment.UserId == currentUserId || User.IsInRole("Admin"))
            {
                _context.Comments.Remove(comment);
                await _context.SaveChangesAsync();
                return RedirectToAction("Show", "Bookmarks", new { id = comment.BookmarkId });
            }

            return Forbid();
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Edit(int id)
        {
            var comment = await _context.Comments.FindAsync(id);

            if (comment == null)
            {
                return NotFound();
            }

            var currentUserId = _userManager.GetUserId(User);

            // Verificare strictă: Doar autorul poate edita
            if (comment.UserId != currentUserId)
            {
                return Forbid(); // Returnează eroare 403 Forbidden
            }

            return View(comment);
        }

        // POST: Procesează modificarea
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [FromForm] Comment requestComment)
        {
            if (id != requestComment.Id)
            {
                return NotFound();
            }

            var existingComment = await _context.Comments.FindAsync(id);

            if (existingComment == null)
            {
                return NotFound();
            }

            var currentUserId = _userManager.GetUserId(User);

            if (existingComment.UserId != currentUserId)
            {
                return Forbid();
            }

            ModelState.Remove("User");
            ModelState.Remove("Bookmark");
            ModelState.Remove("UserId");

            if (ModelState.IsValid)
            {
                existingComment.Content = requestComment.Content;

                _context.Comments.Update(existingComment);
                await _context.SaveChangesAsync();

                return RedirectToAction("Show", "Bookmarks", new { id = existingComment.BookmarkId });
            }

            return View(requestComment);
        }
    }
}