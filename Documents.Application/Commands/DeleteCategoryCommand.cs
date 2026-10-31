using Documents.Application.Common;
using Documents.Core.Interfaces;
using Documents.Core.Models;
using MediatR;

namespace Documents.Application.Commands
{
    /// <summary>
    /// Apni category hatana = deactivate (spec 013). Active documents hon to 409 — pehle unki category badlo.
    /// Soft-deleted documents ki FK rehti hai, isliye row kabhi delete nahi hoti.
    /// </summary>
    public record DeleteCategoryCommand(Guid UserId, Guid Id) : IRequest<Result>;

    public class DeleteCategoryCommandHandler(ICategoryRepository categories) : IRequestHandler<DeleteCategoryCommand, Result>
    {
        public async Task<Result> Handle(DeleteCategoryCommand command, CancellationToken ct)
        {
            var category = await categories.GetVisibleAsync(command.UserId, command.Id, ct);
            if (category is null) return Result.Fail(ErrorKind.NotFound, UpdateCategoryCommandHandler.NotFoundMessage);
            if (category.UserId is null) return Result.Fail(ErrorKind.Forbidden, LookupValidation.SystemMessage);
            // 404 / 403 upar ho chuke; yahan false = use mein hai
            return await categories.DeactivateIfUnusedAsync(command.UserId, command.Id, ct)
                ? Result.Success()
                : Result.Fail(ErrorKind.Conflict, "This category has documents. Move them to another category first.");
        }
    }
}
