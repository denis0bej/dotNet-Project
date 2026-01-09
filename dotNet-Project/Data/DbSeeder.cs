using dotNet_Project.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore; // Necesar pentru interactiunea cu DB
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
            // Nota: Trebuie sa le asignam si User-ul corect daca vrem sa apara in lista lor

            // Preluam userii proaspat creati (sau existenti)
            var currentAdmin = await userManager.FindByEmailAsync(adminEmail);
            var currentUser = await userManager.FindByEmailAsync(userEmail);

            if (!context.Categories.Any())
            {
                context.Categories.AddRange(
                    new Category { Name = "Valorant", UserId = currentAdmin.Id }, // Admin category
                    new Category { Name = "Tree", UserId = currentUser.Id },      // User category
                    new Category { Name = "Cats", UserId = currentUser.Id }       // User category
                );
                await context.SaveChangesAsync();
            }

            // 5. Adăugăm Bookmark-uri (ACTUALIZAT PENTRU MANY-TO-MANY)
            if (!context.Bookmarks.Any())
            {
                // Preluăm categoriile din DB (trebuie sa fie obiecte track-uite de EF Core)
                var catValorant = context.Categories.First(c => c.Name == "Valorant");
                var catTree = context.Categories.First(c => c.Name == "Tree");
                var catCats = context.Categories.First(c => c.Name == "Cats");

                var bookmarks = new List<Bookmark>();

                // Bookmark-uri pentru Admin
                if (currentAdmin != null)
                {
                    bookmarks.Add(new Bookmark
                    {
                        Title = "Ghid complet Valorant",
                        Description = "Tactici avansate pentru jocul competitiv.",
                        UserId = currentAdmin.Id,
                        // MODIFICARE AICI: Folosim lista Categories, nu CategoryId
                        Categories = new List<Category> { catValorant },
                        CreatedAt = DateTime.Now,
                        MediaUrl = "/images/valorant1.jpg",
                        IsPublic = true
                    });

                    bookmarks.Add(new Bookmark
                    {
                        Title = "Lineups Sova",
                        Description = "Săgeți utile pentru harta Ascent.",
                        UserId = currentAdmin.Id,
                        Categories = new List<Category> { catValorant },
                        CreatedAt = DateTime.Now.AddDays(-1),
                        MediaUrl = "/images/valorant2.jpg",
                        IsPublic = true
                    });
                }

                // Bookmark-uri pentru User-ul obișnuit
                if (currentUser != null)
                {
                    bookmarks.Add(new Bookmark
                    {
                        Title = "Poze amuzante cu pisici",
                        Description = "O listă cu cele mai haioase pisici de pe internet.",
                        UserId = currentUser.Id,
                        Categories = new List<Category> { catCats },
                        CreatedAt = DateTime.Now.AddHours(-5),
                        MediaUrl = "/images/cat1.jpg",
                        IsPublic = true
                    });

                    // Exemplu de putere Many-to-Many: 
                    // Un bookmark care e si in "Cats" si in "Tree" (Pisica in copac)
                    bookmarks.Add(new Bookmark
                    {
                        Title = "Pisica blocata in copac",
                        Description = "O colecție de salvări de pisici.",
                        UserId = currentUser.Id,
                        // AICI POTI PUNE MAI MULTE CATEGORII
                        Categories = new List<Category> { catTree, catCats },
                        CreatedAt = DateTime.Now.AddDays(-2),
                        MediaUrl = "/images/tree1.jpg",
                        IsPublic = true
                    });
                }

                context.Bookmarks.AddRange(bookmarks);
                await context.SaveChangesAsync();
            }
        }
    }
}