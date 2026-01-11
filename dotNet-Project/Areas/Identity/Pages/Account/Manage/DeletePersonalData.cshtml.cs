using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using dotNet_Project.Data; // Asigura-te ca namespace-ul este corect
using dotNet_Project.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace dotNet_Project.Areas.Identity.Pages.Account.Manage
{
    public class DeletePersonalDataModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ILogger<DeletePersonalDataModel> _logger;
        private readonly ApplicationDbContext _context; // <--- 1. Am adaugat Context-ul

        public DeletePersonalDataModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ILogger<DeletePersonalDataModel> logger,
            ApplicationDbContext context) // <--- 2. Injectam Context-ul
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
            _context = context;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; }
        }

        public bool RequirePassword { get; set; }

        public async Task<IActionResult> OnGet()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            RequirePassword = await _userManager.HasPasswordAsync(user);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            RequirePassword = await _userManager.HasPasswordAsync(user);
            if (RequirePassword)
            {
                if (!await _userManager.CheckPasswordAsync(user, Input.Password))
                {
                    ModelState.AddModelError(string.Empty, "Incorrect password.");
                    return Page();
                }
            }

            // --- FIX PENTRU APPLICATIONDBCONTEXT (RESTRICT/NOACTION) ---
            // Stergem manual dependintele inainte de a sterge utilizatorul
            try
            {
                // 1. Stergem voturile date de acest user
                var votes = _context.Votes.Where(v => v.UserId == user.Id);
                _context.Votes.RemoveRange(votes);

                // 2. Stergem comentariile scrise de acest user
                var comments = _context.Comments.Where(c => c.UserId == user.Id);
                _context.Comments.RemoveRange(comments);

                // 3. (Optional) Daca vrei sa stergi si tag-urile orfane sau alte date, aici e locul.
                // Bookmarks-urile detinute de user se vor sterge automat prin Cascade (implicit),
                // dar Voturile si Comentariile erau blocate de regula Restrict.

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Logam eroarea daca ceva nu merge la curatare
                _logger.LogError(ex, "Eroare la stergerea datelor dependente pentru userul {UserId}", user.Id);
                throw; // Sau returnam o eroare catre pagina
            }
            // -----------------------------------------------------------

            var result = await _userManager.DeleteAsync(user);
            var userId = await _userManager.GetUserIdAsync(user);

            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Unexpected error occurred deleting user.");
            }

            await _signInManager.SignOutAsync();
            _logger.LogInformation("User with ID '{UserId}' deleted themselves.", userId);

            return Redirect("~/");
        }
    }
}