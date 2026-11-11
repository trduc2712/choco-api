using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Reports;
using Choco.Domain.Posts;
using Choco.Domain.Primitives.Persistence;
using Choco.Domain.Reports;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Reports.ReportPost;

internal sealed class ReportPostCommandHandler(
    ICurrentUser currentUser,
    IPostRepository postRepository,
    IUserRepository userRepository,
    IReportRepository reportRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<ReportPostCommand>
{
    public async Task<Result> Handle(ReportPostCommand command, CancellationToken cancellationToken)
    {
        Result<Guid> caller = await currentUser.GetIdAsync(cancellationToken);
        if (caller.IsFailure)
        {
            return caller;
        }

        Post? post = await postRepository.GetByIdForReadAsync(command.PostId, cancellationToken);
        if (post is null || await userRepository.GetByIdForReadAsync(post.AuthorId, cancellationToken) is null)
        {
            return Result.Failure(PostErrors.NotFound);
        }

        ReportEnums.TryParse(command.Reason, out ReportReason reason);

        Result<Report> report = Report.CreateForPost(
            caller.Value, post.Id, post.AuthorId, reason, command.Details, timeProvider.GetUtcNow());
        if (report.IsFailure)
        {
            return report;
        }

        await unitOfWork.InTransactionAsync(async () =>
        {
            if (await reportRepository.AddIfNoOpenAsync(report.Value, cancellationToken))
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return true;
        }, cancellationToken);

        return Result.Success();
    }
}
