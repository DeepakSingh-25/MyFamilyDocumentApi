using Documents.Application.Common;
using Documents.Core.Entities;
using Documents.Core.Interfaces;
using Documents.Core.Models;
using Documents.Core.Rules;
using MediatR;

namespace Documents.Application.Commands
{
    /// <summary>User ki apni category (spec 013). Naam system ya apni kisi category se clash na kare.</summary>
    public record CreateCategoryCommand(Guid UserId, CategoryRequest Request) : IRequest<Result<DocumentCategoryDto>>;

    public class CreateCategoryCommandHandler(ICategoryRepository categories) : IRequestHandler<CreateCategoryCommand, Result<DocumentCategoryDto>>
    {
        internal const string DuplicateMessage = "A category with this name already exists.";
        private const string CustomIcon = "bi-tag";

        public async Task<Result<DocumentCategoryDto>> Handle(CreateCategoryCommand command, CancellationToken ct)
        {
            var problem = LookupValidation.CheckCategory(command.Request);
            if (problem is not null) return Result.Fail<DocumentCategoryDto>(ErrorKind.Validation, problem);

            var name = command.Request.Name.Trim();
            if (await categories.NameExistsAsync(command.UserId, name, null, ct))
                return Result.Fail<DocumentCategoryDto>(ErrorKind.Conflict, DuplicateMessage);
            if (await categories.CountCustomAsync(command.UserId, ct) >= DomainRules.CustomLookupMax)
                return Result.Fail<DocumentCategoryDto>(ErrorKind.Validation, LookupValidation.TooManyMessage);

            var entity = new DocumentCategoryEntity
            {
                UserId = command.UserId,
                Name = name,
                Icon = CustomIcon,
                Examples = command.Request.Examples?.Trim() ?? "",
                SortOrder = DomainRules.CustomSortOrder,
                IsActive = true,
            };
            if (!await categories.AddAsync(entity, ct))
                return Result.Fail<DocumentCategoryDto>(ErrorKind.Conflict, DuplicateMessage);

            return Result.Success(new DocumentCategoryDto(entity.Id, entity.Name, entity.Icon, entity.Examples, IsSystem: false, entity.SortOrder));
        }
    }
}
