using Documents.Application.Common;
using Documents.Core.Interfaces;
using Documents.Core.Models;
using MediatR;

namespace Documents.Application.Commands
{
    /// <summary>Details update; File diya ho to stored file replace hoti hai.</summary>
    public record UpdateDocumentCommand(Guid UserId, Guid Id, DocumentDetails Details, UploadedFile? File) : IRequest<Result<FamilyDocumentDto>>;

    public class UpdateDocumentCommandHandler(
        IDocumentRepository documents,
        IFamilyMemberRepository members,
        ICategoryRepository categories,
        IFileStorage storage,
        TimeProvider clock) : IRequestHandler<UpdateDocumentCommand, Result<FamilyDocumentDto>>
    {
        private const string NotFoundMessage = "Document not found.";

        public async Task<Result<FamilyDocumentDto>> Handle(UpdateDocumentCommand command, CancellationToken ct)
        {
            var entity = await documents.GetForUpdateAsync(command.UserId, command.Id, ct);
            if (entity is null) return Result.Fail<FamilyDocumentDto>(ErrorKind.NotFound, NotFoundMessage);

            var problem = await new DocumentValidation(members, categories).CheckDetailsAsync(command.UserId, command.Details, ct);
            if (problem is not null) return Result.Fail<FamilyDocumentDto>(ErrorKind.Validation, problem);

            string? newKey = null;
            string? oldKey = null;
            if (command.File is not null)
            {
                var (fileType, fileError) = await DocumentValidation.CheckFileAsync(command.File, ct);
                if (fileError is not null) return Result.Fail<FamilyDocumentDto>(ErrorKind.Validation, fileError);

                // Pehle nayi file likho, DB update ke baad hi purani hatao
                newKey = await storage.SaveAsync(command.File.Content, fileType!, ct);
                oldKey = entity.StorageKey;
                entity.StorageKey = newKey;
                entity.FileName = DocumentValidation.SafeFileName(command.File.FileName);
                entity.FileType = fileType!;
                entity.FileSize = command.File.Length;
            }

            var (name, hospital, notes) = DocumentValidation.Clean(command.Details);
            entity.Name = name;
            entity.MemberId = command.Details.MemberId;
            entity.CategoryId = command.Details.CategoryId;
            entity.DocumentDate = command.Details.DocumentDate;
            entity.HospitalOrDoctor = hospital;
            entity.Notes = notes;
            entity.UpdatedOn = clock.GetUtcNow().UtcDateTime;

            try
            {
                await documents.SaveChangesAsync(ct);
            }
            catch
            {
                if (newKey is not null) storage.Delete(newKey);
                throw;
            }

            if (oldKey is not null) storage.Delete(oldKey);

            var dto = await documents.GetAsync(command.UserId, command.Id, ct);
            return dto is null ? Result.Fail<FamilyDocumentDto>(ErrorKind.NotFound, NotFoundMessage) : Result.Success(dto);
        }
    }
}
