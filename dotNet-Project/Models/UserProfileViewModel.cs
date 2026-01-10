using System.Collections.Generic;

namespace dotNet_Project.Models
{
    public class UserProfileViewModel
    {
        public string UserId { get; set; }
        public string FullName { get; set; }
        public string Description { get; set; }
        public string ProfilePicture { get; set; }

        // Verificam daca cel care se uita la profil este proprietarul
        public bool IsOwner { get; set; }

        // Lista de categorii (foldere)
        public List<Category> Categories { get; set; } = new List<Category>();
    }
}