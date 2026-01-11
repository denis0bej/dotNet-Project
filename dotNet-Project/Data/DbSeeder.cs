using dotNet_Project.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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

            // =============================================================
            // 1. ROLURI
            // =============================================================
            string[] roleNames = { "Admin", "User" };
            foreach (var roleName in roleNames)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            // =============================================================
            // 2. UTILIZATORI
            // =============================================================
            var password = "ParolaUser1!";

            var usersData = new List<ApplicationUser>
            {
                new ApplicationUser { UserName = "admin@test.com", Email = "admin@test.com", FirstName = "System", LastName = "Admin", Description = "Eu administrez platforma.", ProfilePicture = "https://ui-avatars.com/api/?name=System+Admin&background=0D8ABC&color=fff" },
                new ApplicationUser { UserName = "user@test.com", Email = "user@test.com", FirstName = "Normal", LastName = "User", Description = "Doar un utilizator obișnuit.", ProfilePicture = "https://ui-avatars.com/api/?name=Normal+User&background=random" },
                
                // Am pus linkuri de profil sigure
                new ApplicationUser { UserName = "alice@art.com", Email = "alice@art.com", FirstName = "Alice", LastName = "Photographer", Description = "Visual storyteller. Iubesc fotografia pe film și natura.", ProfilePicture = "https://picsum.photos/id/64/150/150.jpg" },
                new ApplicationUser { UserName = "bob@tech.com", Email = "bob@tech.com", FirstName = "Bob", LastName = "Developer", Description = "Full Stack Dev. C# & React enthusiast.", ProfilePicture = "https://picsum.photos/id/1/150/150.jpg" },
                new ApplicationUser { UserName = "charlie@travel.com", Email = "charlie@travel.com", FirstName = "Charlie", LastName = "Traveler", Description = "Explorând lumea, un oraș pe rând.", ProfilePicture = "https://picsum.photos/id/1005/150/150.jpg" }
            };

            foreach (var userData in usersData)
            {
                var user = await userManager.FindByEmailAsync(userData.Email);
                if (user == null)
                {
                    userData.EmailConfirmed = true;
                    var result = await userManager.CreateAsync(userData, password);
                    if (result.Succeeded)
                    {
                        var role = userData.Email == "admin@test.com" ? "Admin" : "User";
                        await userManager.AddToRoleAsync(userData, role);
                    }
                }
            }

            var admin = await userManager.FindByEmailAsync("admin@test.com");
            var userNormal = await userManager.FindByEmailAsync("user@test.com");
            var alice = await userManager.FindByEmailAsync("alice@art.com");
            var bob = await userManager.FindByEmailAsync("bob@tech.com");
            var charlie = await userManager.FindByEmailAsync("charlie@travel.com");

            // =============================================================
            // 3. TAG-URI
            // =============================================================
            if (!context.Tags.Any())
            {
                context.Tags.AddRange(
                    new Tag { Name = "Photography" },
                    new Tag { Name = "Nature" },
                    new Tag { Name = "Coding" },
                    new Tag { Name = "CSharp" },
                    new Tag { Name = "Travel" },
                    new Tag { Name = "Food" },
                    new Tag { Name = "Minimalism" },
                    new Tag { Name = "Tech" }
                );
                await context.SaveChangesAsync();
            }

            var tagPhoto = context.Tags.First(t => t.Name == "Photography");
            var tagNature = context.Tags.First(t => t.Name == "Nature");
            var tagCoding = context.Tags.First(t => t.Name == "Coding");
            var tagCSharp = context.Tags.First(t => t.Name == "CSharp");
            var tagTravel = context.Tags.First(t => t.Name == "Travel");
            var tagFood = context.Tags.First(t => t.Name == "Food");
            var tagTech = context.Tags.First(t => t.Name == "Tech");
            var tagMinimalism = context.Tags.First(t => t.Name == "Minimalism");

            // =============================================================
            // 4. CATEGORII
            // =============================================================
            if (!context.Categories.Any())
            {
                context.Categories.AddRange(
                    new Category { Name = "Official News", UserId = admin.Id, IsPublic = true },
                    new Category { Name = "Portofoliu Foto", UserId = alice.Id, IsPublic = true },
                    new Category { Name = "Inspiratie", UserId = alice.Id, IsPublic = false },
                    new Category { Name = ".NET Resources", UserId = bob.Id, IsPublic = true },
                    new Category { Name = "Code Snippets", UserId = bob.Id, IsPublic = true },
                    new Category { Name = "Vacante 2024", UserId = charlie.Id, IsPublic = true },
                    new Category { Name = "Restaurante", UserId = charlie.Id, IsPublic = true }
                );
                await context.SaveChangesAsync();
            }

            var catNews = context.Categories.First(c => c.Name == "Official News");
            var catFoto = context.Categories.First(c => c.Name == "Portofoliu Foto");
            var catDotNet = context.Categories.First(c => c.Name == ".NET Resources");
            var catVacante = context.Categories.First(c => c.Name == "Vacante 2024");
            var catRestaurante = context.Categories.First(c => c.Name == "Restaurante");

            // =============================================================
            // 5. BOOKMARK-URI (CU LINK-URI VALIDATE .JPG)
            // =============================================================
            if (!context.Bookmarks.Any())
            {
                var bookmarks = new List<Bookmark>
                {
                    new Bookmark
                    {
                        Title = "Apus în Munții Apuseni",
                        Description = "O fotografie surprinsă weekend-ul trecut. Lumina a fost perfectă.",
                        UserId = alice.Id,
                        // Folosim Picsum cu extensia .jpg la final pentru a trece de validarea din View
                        MediaUrl = "https://picsum.photos/id/10/800/600.jpg",
                        CreatedAt = DateTime.Now.AddDays(-5),
                        IsPublic = true,
                        Categories = new List<Category> { catFoto },
                        Tags = new List<Tag> { tagPhoto, tagNature, tagTravel }
                    },
                    new Bookmark
                    {
                        Title = "Ghid Clean Architecture în .NET",
                        Description = "Cele mai bune practici pentru structurarea proiectelor mari.",
                        UserId = bob.Id,
                        MediaUrl = "https://picsum.photos/id/180/800/600.jpg", // Imagine cu laptop
                        CreatedAt = DateTime.Now.AddDays(-2),
                        IsPublic = true,
                        Categories = new List<Category> { catDotNet },
                        Tags = new List<Tag> { tagCoding, tagCSharp, tagTech }
                    },
                    new Bookmark
                    {
                        Title = "Cel mai bun Ramen din Tokyo",
                        Description = "Am găsit acest loc micuț în Shinjuku. Trebuie neapărat să încercați!",
                        UserId = charlie.Id,
                        MediaUrl = "https://picsum.photos/id/292/800/600.jpg", // Imagine cu mancare
                        CreatedAt = DateTime.Now.AddHours(-10),
                        IsPublic = true,
                        Categories = new List<Category> { catVacante, catRestaurante },
                        Tags = new List<Tag> { tagTravel, tagFood }
                    },
                    new Bookmark
                    {
                        Title = "Platform Update v2.0",
                        Description = "Am adăugat funcționalități noi: Dark Mode, Search avansat și AI Suggestions.",
                        UserId = admin.Id,
                        MediaUrl = "https://picsum.photos/id/119/800/600.jpg", // Imagine cu birou/mac
                        CreatedAt = DateTime.Now.AddDays(-10),
                        IsPublic = true,
                        Categories = new List<Category> { catNews },
                        Tags = new List<Tag> { tagTech }
                    },
                    new Bookmark
                    {
                        Title = "Minimalist Desk Setup",
                        Description = "Spațiul meu de lucru pentru editare foto.",
                        UserId = alice.Id,
                        MediaUrl = "https://picsum.photos/id/3/800/600.jpg", // Imagine setup
                        CreatedAt = DateTime.Now.AddDays(-1),
                        IsPublic = true,
                        Categories = new List<Category> { catFoto },
                        Tags = new List<Tag> { tagPhoto, tagMinimalism }
                    }
                };

                context.Bookmarks.AddRange(bookmarks);
                await context.SaveChangesAsync();
            }

            // =============================================================
            // 6. INTERACȚIUNI
            // =============================================================

            var postApus = context.Bookmarks.First(b => b.Title == "Apus în Munții Apuseni");
            var postTech = context.Bookmarks.First(b => b.Title == "Ghid Clean Architecture în .NET");
            var postRamen = context.Bookmarks.First(b => b.Title == "Cel mai bun Ramen din Tokyo");

            if (!context.Votes.Any())
            {
                context.Votes.AddRange(
                    new Vote { BookmarkId = postApus.Id, UserId = bob.Id },
                    new Vote { BookmarkId = postApus.Id, UserId = charlie.Id },
                    new Vote { BookmarkId = postApus.Id, UserId = userNormal.Id },
                    new Vote { BookmarkId = postTech.Id, UserId = alice.Id },
                    new Vote { BookmarkId = postRamen.Id, UserId = admin.Id }
                );

                postApus.LikeCount = 3;
                postTech.LikeCount = 1;
                postRamen.LikeCount = 1;
                await context.SaveChangesAsync();
            }

            if (!context.Comments.Any())
            {
                context.Comments.AddRange(
                    new Comment { Content = "Wow, culorile sunt incredibile! Ce cameră folosești?", UserId = charlie.Id, BookmarkId = postApus.Id, CreatedAt = DateTime.Now.AddDays(-4) },
                    new Comment { Content = "Mulțumesc! E făcută cu un Sony A7III.", UserId = alice.Id, BookmarkId = postApus.Id, CreatedAt = DateTime.Now.AddDays(-3) },
                    new Comment { Content = "Super util articolul, mersi Bob!", UserId = userNormal.Id, BookmarkId = postTech.Id, CreatedAt = DateTime.Now.AddHours(-2) }
                );
                await context.SaveChangesAsync();
            }
        }
    }
}