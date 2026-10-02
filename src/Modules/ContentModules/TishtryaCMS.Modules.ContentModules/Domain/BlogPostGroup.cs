using TishtryaCMS.SharedKernel;

namespace TishtryaCMS.Modules.ContentModules.Domain;

public sealed class BlogPostGroup : Entity
{
    public Guid PostId { get; private set; }
    public Guid GroupId { get; private set; }

    public BlogPost? Post { get; private set; }
    public BlogGroup? Group { get; private set; }

    private BlogPostGroup()
    {
    }

    public static BlogPostGroup Create(Guid postId, Guid groupId)
    {
        if (postId == Guid.Empty)
        {
            throw new ArgumentException("postId is required.", nameof(postId));
        }

        if (groupId == Guid.Empty)
        {
            throw new ArgumentException("groupId is required.", nameof(groupId));
        }

        return new BlogPostGroup
        {
            Id = Guid.Empty,
            PostId = postId,
            GroupId = groupId
        };
    }
}
