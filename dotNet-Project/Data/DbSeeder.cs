using dotNet_Project.Models;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
                    EmailConfirmed = true,
                    FirstName = "Admin",
                    LastName = "System"
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
                    EmailConfirmed = true,
                    FirstName = "Normal",
                    LastName = "User"
                };

                var result = await userManager.CreateAsync(newUser, "ParolaUser1!");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(newUser, "User");
                }
            }

            // 4. Adăugăm Categoriile
            var categories = new List<Category>
            {
                new Category { Name = "Valorant"},
                new Category { Name = "Tree"},
                new Category { Name = "Cats"}
            };

            foreach (var cat in categories)
            {
                if (!context.Categories.Any(c => c.Name == cat.Name))
                {
                    context.Categories.Add(cat);
                }
            }

            // Salvăm categoriile pentru a le genera ID-urile
            await context.SaveChangesAsync();


            // 5. Adăugăm Bookmark-uri (SECȚIUNE NOUĂ)

            // Verificăm dacă există deja bookmark-uri pentru a nu le dubla
            if (!context.Bookmarks.Any())
            {
                // Preluăm userii din DB pentru a avea ID-urile lor corecte
                var adminInDb = await userManager.FindByEmailAsync(adminEmail);
                var userInDb = await userManager.FindByEmailAsync(userEmail);

                // Preluăm categoriile din DB pentru a avea ID-urile lor
                var catValorant = context.Categories.First(c => c.Name == "Valorant");
                var catTree = context.Categories.First(c => c.Name == "Tree");
                var catCats = context.Categories.First(c => c.Name == "Cats");

                var bookmarks = new List<Bookmark>();

                // Bookmark-uri pentru Admin
                if (adminInDb != null)
                {
                    bookmarks.Add(new Bookmark
                    {
                        Title = "Ghid complet Valorant",
                        Description = "Tactici avansate pentru jocul competitiv.",
                        UserId = adminInDb.Id,
                        CategoryId = catValorant.Id,
                        CreatedAt = DateTime.Now,
                        MediaUrl = "/images/valorant1.jpg"
                    });

                    bookmarks.Add(new Bookmark
                    {
                        Title = "Lineups Sova",
                        Description = "Săgeți utile pentru harta Ascent.",
                        UserId = adminInDb.Id,
                        CategoryId = catValorant.Id,
                        CreatedAt = DateTime.Now.AddDays(-1),
                        MediaUrl = "/images/valorant2.jpg"
                    });
                }

                // Bookmark-uri pentru User-ul obișnuit
                if (userInDb != null)
                {
                    bookmarks.Add(new Bookmark
                    {
                        Title = "Poze amuzante cu pisici",
                        Description = "O listă cu cele mai haioase pisici de pe internet.",
                        UserId = userInDb.Id,
                        CategoryId = catCats.Id,
                        CreatedAt = DateTime.Now.AddHours(-5),
                        MediaUrl = "/images/cat1.jpg"
                    });

                    bookmarks.Add(new Bookmark
                    {
                        Title = "Tipuri de arbori",
                        Description = "O colecție de arbori din America de Nord.",
                        UserId = userInDb.Id,
                        CategoryId = catTree.Id,
                        CreatedAt = DateTime.Now.AddDays(-2), // Creat acum 2 zile
                        MediaUrl = "/images/tree1.jpg"
                    });
                }

                context.Bookmarks.AddRange(bookmarks);
                await context.SaveChangesAsync();
            }
        }
    }
}