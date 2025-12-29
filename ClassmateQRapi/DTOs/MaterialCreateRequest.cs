using System.ComponentModel.DataAnnotations;

namespace ClassmateQRapi.DTOs
{
    public class MaterialCreateRequest
    {
        [Required]
        public string Title { get; set; }

        public string? Description { get; set; }

        // Cho phép upload nhiều file
        public List<IFormFile>? Files { get; set; }
    }
}
