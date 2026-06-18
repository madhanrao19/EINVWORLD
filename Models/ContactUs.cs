using System;
using System.ComponentModel.DataAnnotations;

namespace eInvWorld.Models
{
    public class ContactUs
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public required string Name { get; set; }

        [Required]
        public required string Company { get; set; }

        [Required]
        public required string Telephone { get; set; }

        [Required, EmailAddress]
        public required string Email { get; set; }

        [Required]
        public required string Message { get; set; }

        public DateTime SubmittedAt { get; set; } = DateTime.Now;
    }
}
