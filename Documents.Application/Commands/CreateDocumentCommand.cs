using Documents.Application.Common;
using Documents.Core.Entities;
using Documents.Core.Interfaces;
using Documents.Core.Models;
using MediatR;

namespace Documents.Application.Commands
{
    public record CreateDocumentCommand(Guid UserId, DocumentDetails Details, UploadedFile? File) : IRequest<Result<FamilyDocumentDto>>;

    public class CreateDocumentCommandHandler(
        IDocumentRepository documents,
        IFamilyMemberRepository members,
        ICategoryRepository categories,
        IFileStorage storage,
        TimeProvider clock) : IRequestHandler<CreateDocumentCommand, Result<FamilyDocumentDto>>
    {
        public async Task<Result<FamilyDocumentDto>> Handle(CreateDocumentCommand command, CancellationToken ct)
        {
            var problem = await new DocumentValidation(members, categories).CheckDetailsAsync(command.UserId, command.Details, ct);
            if (problem is not null) return Result.Fail<FamilyDocumentDto>(ErrorKind.Validation, problem);

            if (command.File is null) return Result.Fail<FamilyDocumentDto>(ErrorKind.Validation, "Please choose a file to upload.");
            var (fileType, fileError) = await DocumentValidation.CheckFileAsync(command.File, ct);
            if (fileError is not null) return Result.Fail<FamilyDocumentDto>(ErrorKind.Validation, fileError);

            var key = await storage.SaveAsync(command.File.Content, fileType!, ct);
            var (name, hospital, notes) = DocumentValidation.Clean(command.Details);
            var entity = new DocumentEntity
            {
                UserId = command.UserId,
                MemberId = command.Details.MemberId,
                CategoryId = command.Details.CategoryId,
                Name = name,
                DocumentDate = command.Details.DocumentDate,
                HospitalOrDoctor = hospital,
                Notes = notes,
                FileName = DocumentValidation.SafeFileName(command.File.FileName),
                FileType = fileType!,
                FileSize = command.File.Length,
                StorageKey = key,
                UploadedOn = clock.GetUtcNow().UtcDateTime,
            };

            try
            {
                await documents.AddAsync(entity, ct);
            }
            catch
            {
                // Row save nahi hui to file bhi na bache
                storage.Delete(key);
                throw;
            }

            var dto = await documents.GetAsync(command.UserId, entity.Id, ct);
            // Null = isi beech member soft-delete ho gaya (query filter document chhupa deta hai). Row + file member ke
            // saath rehti hain (restore par wapas aayengi); client ko 500 ki jagah wahi message jo pehle check deta (spec 007 §B.5)
            return dto is null
                ? Result.Fail<FamilyDocumentDto>(ErrorKind.Validation, "Please choose a family member.")
                : Result.Success(dto);
        }
    }
}
