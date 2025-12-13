using dotNet_Project.Models;
using Microsoft.AspNetCore.Identity;

namespace dotNet_Project.Data
{
    public static class DbSeeder
    {
        public static async Task Initialize(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var context = serviceProvider.GetRequiredService<ApplicationDbContext>();

            // 1. Definim rolurile
            string[] roleNames = { "Admin", "User" };

            foreach (var roleName in roleNames)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            // 2. Creăm Admin-ul
            var adminEmail = "admin@test.com";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);

            if (adminUser == null)
            {
                var newAdmin = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(newAdmin, "ParolaAdmin1!");

                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(newAdmin, "Admin");
                }
            }

            // 3. Cream User-ul
            var userEmail = "user@test.com";
            var userUser = await userManager.FindByEmailAsync(userEmail);

            if (userUser == null)
            {
                var newUser = new ApplicationUser
                {
                    UserName = userEmail,
                    Email = userEmail,
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(newUser, "ParolaUser1!");

                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(newUser, "User");
                }
            }

            var categories = new List<Category>
            {
                new Category { Name = "Valorant"},
                new Category { Name = "Tree"},
                new Category { Name = "Cats"}
            };

            // Verificăm fiecare categorie dacă există deja în baza de date
            foreach (var cat in categories)
            {
                // Verificăm dacă există deja o categorie cu acest nume
                if (!context.Categories.Any(c => c.Name == cat.Name))
                {
                    context.Categories.Add(cat);
                }
            }

            await context.SaveChangesAsync();

        }
    }
}