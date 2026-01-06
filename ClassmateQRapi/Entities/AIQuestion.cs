// ClassmateQRapi.Entities/AIQuestion.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ClassmateQRapi.Entities
{
    public class AIQuestion
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Question { get; set; } = null!;

        [Required]
        [Column("OptionA")]
        public string OptionA { get; set; } = null!;

        [Required]
        [Column("OptionB")]
        public string OptionB { get; set; } = null!;

        [Required]
        [Column("OptionC")]
        public string OptionC { get; set; } = null!;

        [Required]
        [Column("OptionD")]
        public string OptionD { get; set; } = null!;

        [Required]
        [Range(0, 3)]
        public int CorrectAnswer { get; set; } // 0=A, 1=B, 2=C, 3=D

        public int OrderIndex { get; set; }

        // Foreign keys
        public int AssignmentId { get; set; }
        public Assignment Assignment { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}