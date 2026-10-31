using Documents.Core.Interfaces;
using Documents.Core.Models;
using MediatR;

namespace Documents.Application.Commands
{
    /// <summary>
    /// Soft delete (spec 007 §B.5): member aur uske documents app se gayab, lekin rows aur files rehti hain.
    /// Restore sirf SQL se — docs/migrations/004-AddMemberSoftDelete.sql.
    /// </summary>
    public record DeleteMemberCommand(Guid UserId, Guid Id) : IRequest<Result>;

    public class DeleteMemberCommandHandler(IFamilyMemberRepository members, TimeProvider clock)
        : IRequestHandler<DeleteMemberCommand, Result>
    {
        public async Task<Result> Handle(DeleteMemberCommand command, CancellationToken ct) =>
            await members.SoftDeleteAsync(command.UserId, command.Id, clock.GetUtcNow().UtcDateTime, ct)
                ? Result.Success()
                : Result.Fail(ErrorKind.NotFound, "Family member not found.");
    }
}
