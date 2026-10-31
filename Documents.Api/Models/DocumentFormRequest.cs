using System.ComponentModel.DataAnnotations;
using Documents.Core.Models;
using Documents.Core.Rules;

namespace Documents.Api.Models
{
    /// <summary>
    /// multipart/form-data body for create / update. Api project mein hai kyunki IFormFile ASP.NET type hai;
    /// controller ise Core ke DocumentDetails + UploadedFile mein badalta hai.
    /// </summary>
    public class DocumentFormRequest
    {
        [Required, MaxLength(DomainRules.DocumentNameMax)]
        public string Name { get; set; } = "";

        public Guid MemberId { get; set; }
        public Guid CategoryId { get; set; }
        public DateOnly? DocumentDate { get; set; }

        [MaxLength(DomainRules.HospitalOrDoctorMax)]
        public string? HospitalOrDoctor { get; set; }

        [MaxLength(DomainRules.NotesMax)]
        public string? Notes { get; set; }

        /// <summary>Create par zaroori; update par optional (diya to file replace).</summary>
        public IFormFile? File { get; set; }

        public DocumentDetails ToDetails() => new(Name, MemberId, CategoryId, DocumentDate, HospitalOrDoctor, Notes);
    }
}
