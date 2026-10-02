using TishtryaCMS.SharedKernel;

namespace TishtryaCMS.Modules.ContentModules.Domain;

public sealed class BlogPostTag : Entity
{
    public Guid PostId { get; private set; }
    public Guid TagId { get; private set; }

    public BlogPost? Post { get; private set; }
    public Tag? Tag { get; private set; }

    private BlogPostTag()
    {
    }

    public static BlogPostTag Create(Guid postId, Guid tagId)
    {
        if (postId == Guid.Empty)
        {
            throw new ArgumentException("postId is required.", nameof(postId));
        }

        if (tagId == Guid.Empty)
        {
            throw new ArgumentException("tagId is required.", nameof(tagId));
        }

        return new BlogPostTag
        {
            Id = Guid.Empty,
            PostId = postId,
            TagId = tagId
        };
    }
}
