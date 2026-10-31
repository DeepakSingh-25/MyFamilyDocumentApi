using Documents.Application.Common;
using Documents.Core.Interfaces;
using Documents.Core.Models;
using MediatR;

namespace Documents.Application.Commands
{
    /// <summary>Apni category ka naam / hint badalna. System → 403, kisi aur ki ya missing → 404 (spec 013).</summary>
    public record UpdateCategoryCommand(Guid UserId, Guid Id, CategoryRequest Request) : IRequest<Result<DocumentCategoryDto>>;

    public class UpdateCategoryCommandHandler(ICategoryRepository categories) : IRequestHandler<UpdateCategoryCommand, Result<DocumentCategoryDto>>
    {
        internal const string NotFoundMessage = "Category not found.";

        public async Task<Result<DocumentCategoryDto>> Handle(UpdateCategoryCommand command, CancellationToken ct)
        {
            var problem = LookupValidation.CheckCategory(command.Request);
            if (problem is not null) return Result.Fail<DocumentCategoryDto>(ErrorKind.Validation, problem);

            var category = await categories.GetVisibleAsync(command.UserId, command.Id, ct);
            if (category is null) return Result.Fail<DocumentCategoryDto>(ErrorKind.NotFound, NotFoundMessage);
            if (category.UserId is null) return Result.Fail<DocumentCategoryDto>(ErrorKind.Forbidden, LookupValidation.SystemMessage);

            var name = command.Request.Name.Trim();
            if (await categories.NameExistsAsync(command.UserId, name, command.Id, ct))
                return Result.Fail<DocumentCategoryDto>(ErrorKind.Conflict, CreateCategoryCommandHandler.DuplicateMessage);

            category.Name = name;
            category.Examples = command.Request.Examples?.Trim() ?? "";
            if (!await categories.SaveAsync(ct))
                return Result.Fail<DocumentCategoryDto>(ErrorKind.Conflict, CreateCategoryCommandHandler.DuplicateMessage);

            return Result.Success(new DocumentCategoryDto(category.Id, category.Name, category.Icon, category.Examples, IsSystem: false, category.SortOrder));
        }
    }
}
