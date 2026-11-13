using Choco.Application.Abstractions.Auditing;
using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Posts;
using Choco.Domain.AuditLogs;
using Choco.Domain.Posts;
using Choco.Domain.Primitives.Persistence;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Posts.EditPost;

internal sealed class EditPostCommandHandler : ICommandHandler<EditPostCommand, PostResponse>
{
    private readonly ICurrentUser _currentUser;
    private readonly IPostRepository _postRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly PostResponseReader _reader;
    private readonly AuditChanges _auditChanges;

    public EditPostCommandHandler(
        ICurrentUser currentUser,
        IPostRepository postRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        PostResponseReader reader,
        AuditChanges auditChanges)
    {
        _currentUser = currentUser;
        _postRepository = postRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _reader = reader;
        _auditChanges = auditChanges;
    }

    public async Task<Result<PostResponse>> Handle(EditPostCommand command, CancellationToken cancellationToken)
    {
        Result<Guid> editorIdResult = await _currentUser.GetIdAsync(cancellationToken);

        if (editorIdResult.IsFailure)
        {
            return Result<PostResponse>.Failure(editorIdResult.Error);
        }

        Post? post = await _postRepository.GetByIdAsync(command.Id, cancellationToken);

        if (post is null)
        {
            return Result<PostResponse>.Failure(PostErrors.NotFound);
        }

        User? author = await _userRepository.GetByIdForReadAsync(post.AuthorId, cancellationToken);

        if (author is null)
        {
            return Result<PostResponse>.Failure(PostErrors.NotFound);
        }

        string previousContent = post.Content;
        Result editResult = post.Edit(command.Content, editorIdResult.Value);

        if (editResult.IsFailure)
        {
            return Result<PostResponse>.Failure(editResult.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        Result<PostResponse> result = Result<PostResponse>.Success(
            await _reader.ReadOneAsync(post, author, editorIdResult.Value, cancellationToken));
        return _auditChanges.Record(result, previousContent == post.Content ? [] : [new AuditChange("content")]);
    }
}
