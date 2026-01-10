using System.ComponentModel.DataAnnotations;

namespace dotNet_Project.Models
{
    public class ProfileDetailsModel
    {
        [Display(Name = "First Name")]
        public string? FirstName { get; set; }

        [Display(Name = "Last Name")]
        public string? LastName { get; set; }

        [Display(Name = "About Me")]
        public string? Description { get; set; }

        [Display(Name = "Profile Picture")]
        public IFormFile? ProfileImage { get; set; } // Proprietatea pentru upload

        public string? CurrentProfilePicture { get; set; } // Proprietatea pentru afisare
    }
}