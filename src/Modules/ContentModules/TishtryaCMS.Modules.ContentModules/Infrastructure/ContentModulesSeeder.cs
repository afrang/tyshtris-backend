using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TishtryaCMS.Modules.ContentModules.Domain;

namespace TishtryaCMS.Modules.ContentModules.Infrastructure;

public static class ContentModulesSeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ContentModulesDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("ContentModulesSeeder");

        await EnsureContentTablesAsync(db, logger, cancellationToken);
        await SeedTopMenuAsync(db, logger, cancellationToken);
        await SeedCivilizationAndHistoryBlogGroupAsync(db, logger, cancellationToken);
        await SeedCivilizationAndHistoryMenuItemsAsync(db, logger, cancellationToken);
        await SeedPoliticsAndVisionBlogGroupAsync(db, logger, cancellationToken);
        await SeedPoliticsAndVisionMenuItemsAsync(db, logger, cancellationToken);
        await SeedImperiaDevelopmentBlogGroupAsync(db, logger, cancellationToken);
        await SeedImperiaDevelopmentMenuItemsAsync(db, logger, cancellationToken);
    }

    private static async Task SeedTopMenuAsync(
        ContentModulesDbContext db,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var group = await db.MenuGroups.FirstOrDefaultAsync(x => x.Key == "topmenu", cancellationToken);
        if (group is null)
        {
            group = MenuGroup.Create(
                title: "Top Menu",
                key: "topmenu",
                description: "Main header navigation",
                sortOrder: 0,
                isActive: true);
            db.MenuGroups.Add(group);
            await db.SaveChangesAsync(cancellationToken);

            db.MenuGroupTranslations.Add(MenuGroupTranslation.Create(group.Id, "en", "Top Menu", "Main header navigation"));
            db.MenuGroupTranslations.Add(MenuGroupTranslation.Create(group.Id, "fa", "منوی بالا", "منوی اصلی هدر"));
            db.MenuGroupTranslations.Add(MenuGroupTranslation.Create(group.Id, "ar", "القائمة العلوية", "قائمة الترويسة الرئيسية"));
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded menu group key=topmenu.");
        }

        if (await db.MenuItems.AnyAsync(x => x.GroupId == group.Id, cancellationToken))
        {
            return;
        }

        var seedItems = new (string En, string Fa, string Ar, string Url, int Sort)[]
        {
            ("News", "اخبار", "أخبار", "/news", 0),
            ("Multimedia", "چندرسانه‌ای", "وسائط متعددة", "/multimedia", 1),
            ("Spotlights", "ویژه‌ها", "أضواء", "/spotlights", 2),
            ("About", "درباره", "حول", "/about", 3),
            ("Resources", "منابع", "موارد", "/resources", 4),
        };

        foreach (var item in seedItems)
        {
            var menuItem = MenuItem.Create(
                title: item.En,
                url: item.Url,
                groupId: group.Id,
                parentId: null,
                data: null,
                function: MenuLinkFunction.Custom,
                targetId: null,
                isMegaMenu: false,
                sortOrder: item.Sort,
                isActive: true);
            db.MenuItems.Add(menuItem);
            await db.SaveChangesAsync(cancellationToken);

            db.MenuItemTranslations.Add(MenuItemTranslation.Create(menuItem.Id, "en", item.En, item.Url, null));
            db.MenuItemTranslations.Add(MenuItemTranslation.Create(menuItem.Id, "fa", item.Fa, item.Url, null));
            db.MenuItemTranslations.Add(MenuItemTranslation.Create(menuItem.Id, "ar", item.Ar, item.Url, null));
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded default topmenu items.");
    }

    private static async Task SeedCivilizationAndHistoryMenuItemsAsync(
        ContentModulesDbContext db,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var menuGroup = await db.MenuGroups
            .FirstOrDefaultAsync(x => x.Key == "topmenu", cancellationToken);
        if (menuGroup is null)
        {
            logger.LogWarning("topmenu MenuGroup not found; skipping civilization menu seed.");
            return;
        }

        var blogGroupTr = await db.BlogGroupTranslations
            .FirstOrDefaultAsync(t => t.LanguagePrefix == "fa" && t.Slug == "tamadon-o-tarikh", cancellationToken);
        if (blogGroupTr is null)
        {
            logger.LogWarning("BlogGroup tamadon-o-tarikh not found; skipping civilization menu seed.");
            return;
        }

        Guid blogGroupId = blogGroupTr.GroupId;

        var postSlugsWithInfo = new (string FaSlug, string FaTitle, string EnTitle, string ArTitle, string EnSlug, string ArSlug)[]
        {
            ("hakhmaneshyian-o-takht-jamshid", "هخامنشیان و تخت‌جمشید", "Achaemenids and Persepolis", "الأخمينيديين وبرسپوليس", "achaemenids-and-persepolis", "al-akhminidiyin-wa-barsabulis"),
            ("kourosh-e-bozorg", "کوروش بزرگ", "Cyrus the Great", "كوروش الكبير", "cyrus-the-great", "kurush-al-kabir"),
            ("honar-o-me'mari", "هنر و معماری", "Art and Architecture", "الفنون والهندسة المعمارية", "art-and-architecture", "al-funun-wa-al-handasah-al-maimariyah"),
            ("miras-e-farhangi", "میراث فرهنگی", "Cultural Heritage", "التراث الثقافي", "cultural-heritage", "al-turath-al-thaqafi"),
            ("zaban-o-adabiat", "زبان و ادبیات", "Language and Literature", "اللغة والأدب", "language-and-literature", "al-lughah-wa-al-adab"),
            ("muzeh-o-bastan-shenasi", "موزه و باستان‌شناسی", "Museums and Archaeology", "المتاحف والآثار", "museums-and-archaeology", "al-mutahaf-wa-ilm-al-athar"),
        };

        var postTranslations = await db.BlogPostTranslations
            .Where(t => t.LanguagePrefix == "fa" && postSlugsWithInfo.Select(p => p.FaSlug).Contains(t.Slug))
            .ToListAsync(cancellationToken);
        var postIdsByFaSlug = postTranslations.ToDictionary(t => t.Slug, t => t.PostId);

        var parentMenuItem = await db.MenuItems
            .FirstOrDefaultAsync(x =>
                x.GroupId == menuGroup.Id &&
                (x.Function == MenuLinkFunction.GroupBlog && x.TargetId == blogGroupId ||
                 x.Url == "/civilization"),
                cancellationToken);

        Guid parentId;
        if (parentMenuItem is null)
        {
            var pItem = MenuItem.Create(
                title: "تمدن و تاریخ",
                url: "/civilization",
                groupId: menuGroup.Id,
                parentId: null,
                data: null,
                function: MenuLinkFunction.GroupBlog,
                targetId: blogGroupId,
                isMegaMenu: false,
                sortOrder: 10,
                isActive: true);
            db.MenuItems.Add(pItem);
            await db.SaveChangesAsync(cancellationToken);
            parentId = pItem.Id;

            db.MenuItemTranslations.Add(MenuItemTranslation.Create(parentId, "fa", "تمدن و تاریخ", "/civilization", null));
            db.MenuItemTranslations.Add(MenuItemTranslation.Create(parentId, "en", "Civilization and History", "/civilization", null));
            db.MenuItemTranslations.Add(MenuItemTranslation.Create(parentId, "ar", "الحضارة والتاريخ", "/civilization", null));
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded parent menu item for Civilization and History.");
        }
        else
        {
            parentId = parentMenuItem.Id;
            if (parentMenuItem.Function != MenuLinkFunction.GroupBlog || parentMenuItem.TargetId != blogGroupId)
            {
                parentMenuItem.Update(
                    parentMenuItem.Title,
                    parentMenuItem.Url,
                    parentMenuItem.ParentId,
                    parentMenuItem.Data,
                    MenuLinkFunction.GroupBlog,
                    blogGroupId,
                    parentMenuItem.IsMegaMenu,
                    parentMenuItem.SortOrder,
                    parentMenuItem.IsActive);
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        int sortIndex = 0;
        foreach (var info in postSlugsWithInfo)
        {
            if (!postIdsByFaSlug.TryGetValue(info.FaSlug, out var postId))
            {
                logger.LogWarning("Post with fa slug {Slug} not found, skipping menu seed.", info.FaSlug);
                sortIndex++;
                continue;
            }

            var faUrl = $"/blog/{info.FaSlug}";
            var enUrl = $"/blog/{info.EnSlug}";
            var arUrl = $"/blog/{info.ArSlug}";

            var existingChild = await db.MenuItems.FirstOrDefaultAsync(x =>
                x.GroupId == menuGroup.Id &&
                x.ParentId == parentId &&
                x.Function == MenuLinkFunction.Post &&
                x.TargetId == postId,
                cancellationToken);

            if (existingChild is not null)
            {
                if (existingChild.SortOrder != sortIndex)
                {
                    existingChild.Update(
                        existingChild.Title,
                        existingChild.Url,
                        existingChild.ParentId,
                        existingChild.Data,
                        existingChild.Function,
                        existingChild.TargetId,
                        existingChild.IsMegaMenu,
                        sortIndex,
                        existingChild.IsActive);
                    await db.SaveChangesAsync(cancellationToken);
                }
                sortIndex++;
                continue;
            }

            var child = MenuItem.Create(
                title: info.FaTitle,
                url: faUrl,
                groupId: menuGroup.Id,
                parentId: parentId,
                data: null,
                function: MenuLinkFunction.Post,
                targetId: postId,
                isMegaMenu: false,
                sortOrder: sortIndex,
                isActive: true);
            db.MenuItems.Add(child);
            await db.SaveChangesAsync(cancellationToken);

            db.MenuItemTranslations.Add(MenuItemTranslation.Create(child.Id, "fa", info.FaTitle, faUrl, null));
            db.MenuItemTranslations.Add(MenuItemTranslation.Create(child.Id, "en", info.EnTitle, enUrl, null));
            db.MenuItemTranslations.Add(MenuItemTranslation.Create(child.Id, "ar", info.ArTitle, arUrl, null));
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded menu child: {FaTitle} under Civilization and History.", info.FaTitle);
            sortIndex++;
        }
    }

    private static async Task EnsureContentTablesAsync(
        ContentModulesDbContext db,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync("""
            IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'content')
                EXEC(N'CREATE SCHEMA [content]');
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[content].[BlogGroups]', N'U') IS NULL
            BEGIN
                CREATE TABLE [content].[BlogGroups]
                (
                    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_BlogGroups_id] DEFAULT (NEWSEQUENTIALID()),
                    [title] NVARCHAR(200) NOT NULL,
                    [slug] NVARCHAR(200) NOT NULL,
                    [keyword] NVARCHAR(500) NULL,
                    [description] NVARCHAR(MAX) NULL,
                    [parent_id] UNIQUEIDENTIFIER NULL,
                    CONSTRAINT [PK_BlogGroups] PRIMARY KEY ([id]),
                    CONSTRAINT [UQ_BlogGroups_slug] UNIQUE ([slug]),
                    CONSTRAINT [FK_BlogGroups_BlogGroups_parent_id]
                        FOREIGN KEY ([parent_id]) REFERENCES [content].[BlogGroups] ([id])
                );

                CREATE INDEX [IX_BlogGroups_parent_id] ON [content].[BlogGroups] ([parent_id]);
            END
            """, cancellationToken);

        // Drop legacy single-FK Posts table if present.
        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[content].[Posts]', N'U') IS NOT NULL
                DROP TABLE [content].[Posts];
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[content].[BlogPosts]', N'U') IS NULL
            BEGIN
                CREATE TABLE [content].[BlogPosts]
                (
                    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_BlogPosts_id] DEFAULT (NEWSEQUENTIALID()),
                    [title] NVARCHAR(255) NOT NULL,
                    [slug] NVARCHAR(255) NOT NULL,
                    [keyword] NVARCHAR(MAX) NULL,
                    [description] NVARCHAR(MAX) NULL,
                    [content] NVARCHAR(MAX) NULL,
                    [meta_title] NVARCHAR(255) NULL,
                    [meta_description] NVARCHAR(MAX) NULL,
                    [status] NVARCHAR(20) NOT NULL,
                    [comments_enabled] BIT NOT NULL CONSTRAINT [DF_BlogPosts_comments_enabled] DEFAULT (0),
                    [created_at] DATETIME2 NOT NULL,
                    [updated_at] DATETIME2 NOT NULL,
                    CONSTRAINT [PK_BlogPosts] PRIMARY KEY ([id]),
                    CONSTRAINT [UQ_BlogPosts_slug] UNIQUE ([slug])
                );
            END
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[content].[BlogPosts]', N'U') IS NOT NULL
               AND COL_LENGTH(N'content.BlogPosts', N'comments_enabled') IS NULL
            BEGIN
                ALTER TABLE [content].[BlogPosts]
                    ADD [comments_enabled] BIT NOT NULL
                        CONSTRAINT [DF_BlogPosts_comments_enabled] DEFAULT (0);
            END
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[content].[Tags]', N'U') IS NULL
            BEGIN
                CREATE TABLE [content].[Tags]
                (
                    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_Tags_id] DEFAULT (NEWSEQUENTIALID()),
                    [title] NVARCHAR(100) NOT NULL,
                    [slug] NVARCHAR(100) NOT NULL,
                    [description] NVARCHAR(MAX) NULL,
                    CONSTRAINT [PK_Tags] PRIMARY KEY ([id]),
                    CONSTRAINT [UQ_Tags_slug] UNIQUE ([slug])
                );
            END
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[content].[BlogPostGroups]', N'U') IS NULL
            BEGIN
                CREATE TABLE [content].[BlogPostGroups]
                (
                    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_BlogPostGroups_id] DEFAULT (NEWSEQUENTIALID()),
                    [post_id] UNIQUEIDENTIFIER NOT NULL,
                    [group_id] UNIQUEIDENTIFIER NOT NULL,
                    CONSTRAINT [PK_BlogPostGroups] PRIMARY KEY ([id]),
                    CONSTRAINT [UQ_BlogPostGroups_post_group] UNIQUE ([post_id], [group_id]),
                    CONSTRAINT [FK_BlogPostGroups_BlogPosts_post_id]
                        FOREIGN KEY ([post_id]) REFERENCES [content].[BlogPosts] ([id]) ON DELETE CASCADE,
                    CONSTRAINT [FK_BlogPostGroups_BlogGroups_group_id]
                        FOREIGN KEY ([group_id]) REFERENCES [content].[BlogGroups] ([id])
                );

                CREATE INDEX [IX_BlogPostGroups_post_id] ON [content].[BlogPostGroups] ([post_id]);
                CREATE INDEX [IX_BlogPostGroups_group_id] ON [content].[BlogPostGroups] ([group_id]);
            END
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[content].[BlogPostTags]', N'U') IS NULL
            BEGIN
                CREATE TABLE [content].[BlogPostTags]
                (
                    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_BlogPostTags_id] DEFAULT (NEWSEQUENTIALID()),
                    [post_id] UNIQUEIDENTIFIER NOT NULL,
                    [tag_id] UNIQUEIDENTIFIER NOT NULL,
                    CONSTRAINT [PK_BlogPostTags] PRIMARY KEY ([id]),
                    CONSTRAINT [UQ_BlogPostTags_post_tag] UNIQUE ([post_id], [tag_id]),
                    CONSTRAINT [FK_BlogPostTags_BlogPosts_post_id]
                        FOREIGN KEY ([post_id]) REFERENCES [content].[BlogPosts] ([id]) ON DELETE CASCADE,
                    CONSTRAINT [FK_BlogPostTags_Tags_tag_id]
                        FOREIGN KEY ([tag_id]) REFERENCES [content].[Tags] ([id])
                );

                CREATE INDEX [IX_BlogPostTags_post_id] ON [content].[BlogPostTags] ([post_id]);
                CREATE INDEX [IX_BlogPostTags_tag_id] ON [content].[BlogPostTags] ([tag_id]);
            END
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[content].[Galleries]', N'U') IS NULL
            BEGIN
                CREATE TABLE [content].[Galleries]
                (
                    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_Galleries_id] DEFAULT (NEWSEQUENTIALID()),
                    [title] NVARCHAR(200) NOT NULL,
                    [slug] NVARCHAR(200) NOT NULL,
                    [keyword] NVARCHAR(500) NULL,
                    [description] NVARCHAR(MAX) NULL,
                    [created_by] UNIQUEIDENTIFIER NULL,
                    [created_at] DATETIME2 NOT NULL,
                    [updated_at] DATETIME2 NOT NULL,
                    CONSTRAINT [PK_Galleries] PRIMARY KEY ([id]),
                    CONSTRAINT [UQ_Galleries_slug] UNIQUE ([slug])
                );

                CREATE INDEX [IX_Galleries_created_by] ON [content].[Galleries] ([created_by]);
            END
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[content].[MenuGroups]', N'U') IS NULL
            BEGIN
                CREATE TABLE [content].[MenuGroups]
                (
                    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_MenuGroups_id] DEFAULT (NEWSEQUENTIALID()),
                    [title] NVARCHAR(200) NOT NULL,
                    [key] NVARCHAR(100) NOT NULL,
                    [description] NVARCHAR(MAX) NULL,
                    [sort_order] INT NOT NULL,
                    [is_active] BIT NOT NULL,
                    CONSTRAINT [PK_MenuGroups] PRIMARY KEY ([id]),
                    CONSTRAINT [UQ_MenuGroups_key] UNIQUE ([key])
                );
            END
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[content].[MenuItems]', N'U') IS NULL
            BEGIN
                CREATE TABLE [content].[MenuItems]
                (
                    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_MenuItems_id] DEFAULT (NEWSEQUENTIALID()),
                    [title] NVARCHAR(200) NOT NULL,
                    [url] NVARCHAR(500) NOT NULL,
                    [parent_id] UNIQUEIDENTIFIER NULL,
                    [group_id] UNIQUEIDENTIFIER NOT NULL,
                    [data] NVARCHAR(MAX) NULL,
                    [function] NVARCHAR(50) NOT NULL,
                    [target_id] UNIQUEIDENTIFIER NULL,
                    [is_mega_menu] BIT NOT NULL CONSTRAINT [DF_MenuItems_is_mega_menu] DEFAULT (0),
                    [sort_order] INT NOT NULL,
                    [is_active] BIT NOT NULL,
                    CONSTRAINT [PK_MenuItems] PRIMARY KEY ([id]),
                    CONSTRAINT [FK_MenuItems_MenuGroups_group_id]
                        FOREIGN KEY ([group_id]) REFERENCES [content].[MenuGroups] ([id]),
                    CONSTRAINT [FK_MenuItems_MenuItems_parent_id]
                        FOREIGN KEY ([parent_id]) REFERENCES [content].[MenuItems] ([id])
                );

                CREATE INDEX [IX_MenuItems_group_id] ON [content].[MenuItems] ([group_id]);
                CREATE INDEX [IX_MenuItems_parent_id] ON [content].[MenuItems] ([parent_id]);
                CREATE INDEX [IX_MenuItems_target_id] ON [content].[MenuItems] ([target_id]);
            END
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[content].[MenuItems]', N'U') IS NOT NULL
               AND COL_LENGTH(N'content.MenuItems', N'is_mega_menu') IS NULL
            BEGIN
                ALTER TABLE [content].[MenuItems]
                    ADD [is_mega_menu] BIT NOT NULL
                        CONSTRAINT [DF_MenuItems_is_mega_menu] DEFAULT (0);
            END
            """, cancellationToken);

        await EnsureTranslationTablesAndMigrateAsync(db, cancellationToken);

        logger.LogInformation(
            "Content module schema ensured (BlogGroups, BlogPosts, Tags, Galleries, MenuGroups, MenuItems, pivots, translations).");
    }

    private static async Task EnsureTranslationTablesAndMigrateAsync(
        ContentModulesDbContext db,
        CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[content].[BlogPostTranslations]', N'U') IS NULL
            BEGIN
                CREATE TABLE [content].[BlogPostTranslations]
                (
                    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_BlogPostTranslations_id] DEFAULT (NEWSEQUENTIALID()),
                    [post_id] UNIQUEIDENTIFIER NOT NULL,
                    [language_prefix] NVARCHAR(20) NOT NULL,
                    [title] NVARCHAR(255) NOT NULL,
                    [slug] NVARCHAR(255) NOT NULL,
                    [keyword] NVARCHAR(MAX) NULL,
                    [description] NVARCHAR(MAX) NULL,
                    [content] NVARCHAR(MAX) NULL,
                    [meta_title] NVARCHAR(255) NULL,
                    [meta_description] NVARCHAR(MAX) NULL,
                    CONSTRAINT [PK_BlogPostTranslations] PRIMARY KEY ([id]),
                    CONSTRAINT [UQ_BlogPostTranslations_post_lang] UNIQUE ([post_id], [language_prefix]),
                    CONSTRAINT [UQ_BlogPostTranslations_lang_slug] UNIQUE ([language_prefix], [slug]),
                    CONSTRAINT [FK_BlogPostTranslations_BlogPosts_post_id]
                        FOREIGN KEY ([post_id]) REFERENCES [content].[BlogPosts] ([id]) ON DELETE CASCADE
                );
            END
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[content].[BlogGroupTranslations]', N'U') IS NULL
            BEGIN
                CREATE TABLE [content].[BlogGroupTranslations]
                (
                    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_BlogGroupTranslations_id] DEFAULT (NEWSEQUENTIALID()),
                    [group_id] UNIQUEIDENTIFIER NOT NULL,
                    [language_prefix] NVARCHAR(20) NOT NULL,
                    [title] NVARCHAR(200) NOT NULL,
                    [slug] NVARCHAR(200) NOT NULL,
                    [keyword] NVARCHAR(500) NULL,
                    [description] NVARCHAR(MAX) NULL,
                    CONSTRAINT [PK_BlogGroupTranslations] PRIMARY KEY ([id]),
                    CONSTRAINT [UQ_BlogGroupTranslations_group_lang] UNIQUE ([group_id], [language_prefix]),
                    CONSTRAINT [UQ_BlogGroupTranslations_lang_slug] UNIQUE ([language_prefix], [slug]),
                    CONSTRAINT [FK_BlogGroupTranslations_BlogGroups_group_id]
                        FOREIGN KEY ([group_id]) REFERENCES [content].[BlogGroups] ([id]) ON DELETE CASCADE
                );
            END
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[content].[TagTranslations]', N'U') IS NULL
            BEGIN
                CREATE TABLE [content].[TagTranslations]
                (
                    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_TagTranslations_id] DEFAULT (NEWSEQUENTIALID()),
                    [tag_id] UNIQUEIDENTIFIER NOT NULL,
                    [language_prefix] NVARCHAR(20) NOT NULL,
                    [title] NVARCHAR(100) NOT NULL,
                    [slug] NVARCHAR(100) NOT NULL,
                    [description] NVARCHAR(MAX) NULL,
                    CONSTRAINT [PK_TagTranslations] PRIMARY KEY ([id]),
                    CONSTRAINT [UQ_TagTranslations_tag_lang] UNIQUE ([tag_id], [language_prefix]),
                    CONSTRAINT [UQ_TagTranslations_lang_slug] UNIQUE ([language_prefix], [slug]),
                    CONSTRAINT [FK_TagTranslations_Tags_tag_id]
                        FOREIGN KEY ([tag_id]) REFERENCES [content].[Tags] ([id]) ON DELETE CASCADE
                );
            END
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[content].[GalleryTranslations]', N'U') IS NULL
            BEGIN
                CREATE TABLE [content].[GalleryTranslations]
                (
                    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_GalleryTranslations_id] DEFAULT (NEWSEQUENTIALID()),
                    [gallery_id] UNIQUEIDENTIFIER NOT NULL,
                    [language_prefix] NVARCHAR(20) NOT NULL,
                    [title] NVARCHAR(200) NOT NULL,
                    [slug] NVARCHAR(200) NOT NULL,
                    [keyword] NVARCHAR(500) NULL,
                    [description] NVARCHAR(MAX) NULL,
                    CONSTRAINT [PK_GalleryTranslations] PRIMARY KEY ([id]),
                    CONSTRAINT [UQ_GalleryTranslations_gallery_lang] UNIQUE ([gallery_id], [language_prefix]),
                    CONSTRAINT [UQ_GalleryTranslations_lang_slug] UNIQUE ([language_prefix], [slug]),
                    CONSTRAINT [FK_GalleryTranslations_Galleries_gallery_id]
                        FOREIGN KEY ([gallery_id]) REFERENCES [content].[Galleries] ([id]) ON DELETE CASCADE
                );
            END
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[content].[MenuGroupTranslations]', N'U') IS NULL
            BEGIN
                CREATE TABLE [content].[MenuGroupTranslations]
                (
                    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_MenuGroupTranslations_id] DEFAULT (NEWSEQUENTIALID()),
                    [group_id] UNIQUEIDENTIFIER NOT NULL,
                    [language_prefix] NVARCHAR(20) NOT NULL,
                    [title] NVARCHAR(200) NOT NULL,
                    [description] NVARCHAR(MAX) NULL,
                    CONSTRAINT [PK_MenuGroupTranslations] PRIMARY KEY ([id]),
                    CONSTRAINT [UQ_MenuGroupTranslations_group_lang] UNIQUE ([group_id], [language_prefix]),
                    CONSTRAINT [FK_MenuGroupTranslations_MenuGroups_group_id]
                        FOREIGN KEY ([group_id]) REFERENCES [content].[MenuGroups] ([id]) ON DELETE CASCADE
                );
            END
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[content].[MenuItemTranslations]', N'U') IS NULL
            BEGIN
                CREATE TABLE [content].[MenuItemTranslations]
                (
                    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_MenuItemTranslations_id] DEFAULT (NEWSEQUENTIALID()),
                    [item_id] UNIQUEIDENTIFIER NOT NULL,
                    [language_prefix] NVARCHAR(20) NOT NULL,
                    [title] NVARCHAR(200) NOT NULL,
                    [url] NVARCHAR(500) NOT NULL,
                    [data] NVARCHAR(MAX) NULL,
                    CONSTRAINT [PK_MenuItemTranslations] PRIMARY KEY ([id]),
                    CONSTRAINT [UQ_MenuItemTranslations_item_lang] UNIQUE ([item_id], [language_prefix]),
                    CONSTRAINT [FK_MenuItemTranslations_MenuItems_item_id]
                        FOREIGN KEY ([item_id]) REFERENCES [content].[MenuItems] ([id]) ON DELETE CASCADE
                );
            END
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            DECLARE @lang NVARCHAR(20) = N'en';
            IF OBJECT_ID(N'[settings].[Languages]', N'U') IS NOT NULL
            BEGIN
                SELECT TOP (1) @lang = LOWER(REPLACE(LTRIM(RTRIM([prefix])), N'_', N'-'))
                FROM [settings].[Languages]
                ORDER BY [name];

                IF @lang IS NULL OR LTRIM(RTRIM(@lang)) = N''
                    SET @lang = N'en';
            END

            INSERT INTO [content].[BlogPostTranslations]
                ([post_id], [language_prefix], [title], [slug], [keyword], [description], [content], [meta_title], [meta_description])
            SELECT
                p.[id], @lang, p.[title], p.[slug], p.[keyword], p.[description], p.[content], p.[meta_title], p.[meta_description]
            FROM [content].[BlogPosts] p
            WHERE NOT EXISTS (
                SELECT 1 FROM [content].[BlogPostTranslations] t WHERE t.[post_id] = p.[id]
            );

            INSERT INTO [content].[BlogGroupTranslations]
                ([group_id], [language_prefix], [title], [slug], [keyword], [description])
            SELECT
                g.[id], @lang, g.[title], g.[slug], g.[keyword], g.[description]
            FROM [content].[BlogGroups] g
            WHERE NOT EXISTS (
                SELECT 1 FROM [content].[BlogGroupTranslations] t WHERE t.[group_id] = g.[id]
            );

            INSERT INTO [content].[TagTranslations]
                ([tag_id], [language_prefix], [title], [slug], [description])
            SELECT
                tg.[id], @lang, tg.[title], tg.[slug], tg.[description]
            FROM [content].[Tags] tg
            WHERE NOT EXISTS (
                SELECT 1 FROM [content].[TagTranslations] t WHERE t.[tag_id] = tg.[id]
            );

            INSERT INTO [content].[GalleryTranslations]
                ([gallery_id], [language_prefix], [title], [slug], [keyword], [description])
            SELECT
                g.[id], @lang, g.[title], g.[slug], g.[keyword], g.[description]
            FROM [content].[Galleries] g
            WHERE NOT EXISTS (
                SELECT 1 FROM [content].[GalleryTranslations] t WHERE t.[gallery_id] = g.[id]
            );

            INSERT INTO [content].[MenuGroupTranslations]
                ([group_id], [language_prefix], [title], [description])
            SELECT
                g.[id], @lang, g.[title], g.[description]
            FROM [content].[MenuGroups] g
            WHERE NOT EXISTS (
                SELECT 1 FROM [content].[MenuGroupTranslations] t WHERE t.[group_id] = g.[id]
            );

            INSERT INTO [content].[MenuItemTranslations]
                ([item_id], [language_prefix], [title], [url], [data])
            SELECT
                i.[id], @lang, i.[title], i.[url], i.[data]
            FROM [content].[MenuItems] i
            WHERE NOT EXISTS (
                SELECT 1 FROM [content].[MenuItemTranslations] t WHERE t.[item_id] = i.[id]
            );
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[content].[BlogPosts]', N'U') IS NOT NULL
               AND EXISTS (
                    SELECT 1 FROM sys.key_constraints
                    WHERE [name] = N'UQ_BlogPosts_slug'
                      AND [parent_object_id] = OBJECT_ID(N'[content].[BlogPosts]'))
            BEGIN
                ALTER TABLE [content].[BlogPosts] DROP CONSTRAINT [UQ_BlogPosts_slug];
            END

            IF OBJECT_ID(N'[content].[BlogGroups]', N'U') IS NOT NULL
               AND EXISTS (
                    SELECT 1 FROM sys.key_constraints
                    WHERE [name] = N'UQ_BlogGroups_slug'
                      AND [parent_object_id] = OBJECT_ID(N'[content].[BlogGroups]'))
            BEGIN
                ALTER TABLE [content].[BlogGroups] DROP CONSTRAINT [UQ_BlogGroups_slug];
            END

            IF OBJECT_ID(N'[content].[Tags]', N'U') IS NOT NULL
               AND EXISTS (
                    SELECT 1 FROM sys.key_constraints
                    WHERE [name] = N'UQ_Tags_slug'
                      AND [parent_object_id] = OBJECT_ID(N'[content].[Tags]'))
            BEGIN
                ALTER TABLE [content].[Tags] DROP CONSTRAINT [UQ_Tags_slug];
            END

            IF OBJECT_ID(N'[content].[Galleries]', N'U') IS NOT NULL
               AND EXISTS (
                    SELECT 1 FROM sys.key_constraints
                    WHERE [name] = N'UQ_Galleries_slug'
                      AND [parent_object_id] = OBJECT_ID(N'[content].[Galleries]'))
            BEGIN
                ALTER TABLE [content].[Galleries] DROP CONSTRAINT [UQ_Galleries_slug];
            END
            """, cancellationToken);
    }

    private static async Task SeedCivilizationAndHistoryBlogGroupAsync(
        ContentModulesDbContext db,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var groupSlugFa = "tamadon-o-tarikh";
        var group = await db.BlogGroups
            .Include(g => g.Translations)
            .FirstOrDefaultAsync(g => g.Slug == groupSlugFa, cancellationToken);

        Guid groupId;
        if (group is null)
        {
            group = BlogGroup.Create(
                title: "تمدن و تاریخ",
                slug: groupSlugFa,
                keyword: "تمدن, تاریخ, ایران, تمدن ایرانی",
                description: "گروه مقالات تمدن و تاریخ ایران",
                parentId: null);
            db.BlogGroups.Add(group);
            await db.SaveChangesAsync(cancellationToken);
            groupId = group.Id;

            db.BlogGroupTranslations.Add(BlogGroupTranslation.Create(
                groupId, "fa", "تمدن و تاریخ", groupSlugFa,
                "تمدن, تاریخ, ایران, تمدن ایرانی", "گروه مقالات تمدن و تاریخ ایران"));
            db.BlogGroupTranslations.Add(BlogGroupTranslation.Create(
                groupId, "en", "Civilization and History", "civilization-and-history",
                "civilization, history, Iran, Iranian civilization", "Articles about Iranian civilization and history"));
            db.BlogGroupTranslations.Add(BlogGroupTranslation.Create(
                groupId, "ar", "الحضارة والتاريخ", "al-hadara-wal-tarikh",
                "حضارة, تاريخ, ايران, حضارة ايرانية", "مقالات عن الحضارة والتاريخ الإيراني"));
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded blog group: Civilization and History (تمدن و تاریخ).");
        }
        else
        {
            groupId = group.Id;
        }

        var faContent1 = """
            <h2>امپراتوری هخامنشی</h2>
            <p>امپراتوری هخامنشی یا امپراتوری پارسی اولین امپراتوری بزرگ ایرانی بود که در سال ۵۵۰ پیش از میلاد توسط کوروش بزرگ بنیان‌گذاری شد. این امپراتوری از رود سند در شرق تا آفریقای شمالی و یونان در غرب گسترده شده و بزرگ‌ترین امپراتوری دوران باستان به حساب می‌آید.</p>
            <h3>درباره تخت‌جمشید</h3>
            <p>تخت‌جمشید یا پرسپولیس نام پایتخت رسمی امپراتوری هخامنشی است که در ۷۰ کیلومتری شمال شیراز قرار دارد. این مجموعه معماری در دوره‌های مختلف توسط داریوش بزرگ، خشایارشا و اردشیر اول ساخته و تکمیل گردیده است.</p>
            <h3>بناها و سازه‌های مهم</h3>
            <ul>
            <li><strong>دروازه ملل:</strong> ورودی بزرگ تخت‌جمشید که توسط خشایارشا ساخته شد.</li>
            <li><strong>آپادانا:</strong> کاخ بزرگ مراسم که داریوش بزرگ آن را بنا نهاد و حاوی نقش‌برجسته‌های تحویل هدایای سرزمین‌های تابع است.</li>
            <li><strong>کاخ صد ستون:</strong> با وسعتی در حدود ۴۳۰۰ متر مربع یکی از بزرگ‌ترین کاخ‌های دوره است.</li>
            <li><strong>قصر تچر:</strong> کاخ خصوصی داریوش بزرگ.</li>
            <li><strong>هشمت‌آباد (هادیش):</strong> کاخ خصوصی خشایارشا.</li>
            </ul>
            <h3>نابودی تخت‌جمشید</h3>
            <p>در سال ۳۳۰ پیش از میلاد، اسکندر مقدونی پس از پیروزی بر داریوش سوم، به تخت‌جمشید لشکر کشید و پس از مدتی اقامت در آنجا، کاخ‌ها را به آتش کشید. آتش کشیدن تخت‌جمشید تا به امروز موضوع بحث پژوهشگران است.</p>
            """;

        var enContent1 = """
            <h2>The Achaemenid Empire</h2>
            <p>The Achaemenid Empire, also known as the First Persian Empire, was the first great Iranian empire founded in 550 BCE by Cyrus the Great. At its height, it stretched from the Indus River in the east to North Africa and Greece in the west, making it the largest empire of the ancient world.</p>
            <h3>About Persepolis</h3>
            <p>Persepolis, literally meaning "the Persian city," was the ceremonial capital of the Achaemenid Empire, located 70 kilometers northeast of Shiraz in modern-day Iran. The construction of this magnificent architectural complex began during the reign of Darius the Great and was completed by his successors Xerxes I and Artaxerxes I.</p>
            <h3>Important Structures</h3>
            <ul>
            <li><strong>Gate of All Nations:</strong> The grand entrance of Persepolis built by Xerxes I.</li>
            <li><strong>Apadana Palace:</strong> The great ceremonial palace started by Darius I, famous for its reliefs depicting tribute bearers from all subject lands.</li>
            <li><strong>Hundred-Column Hall:</strong> One of the largest palaces covering approximately 4,300 square meters.</li>
            <li><strong>Tachara Palace:</strong> The private palace of Darius the Great, also known as the Mirror Hall.</li>
            <li><strong>Hadish Palace:</strong> The private palace of Xerxes I.</li>
            </ul>
            <h3>The Destruction</h3>
            <p>In 330 BCE, after defeating Darius III, Alexander the Great marched on Persepolis. After a period of feasting and revelry, the palaces were set ablaze. The exact reasons behind the burning of Persepolis remain debated by historians to this day.</p>
            """;

        var arContent1 = """
            <h2>الإمبراطورية الأخامنشية</h2>
            <p>الإمبراطورية الأخامنشية، المعروفة أيضاً بالإمبراطورية الفارسية الأولى، هي أول إمبراطورية إيرانية عظيمة أسسها كوروش الكبير في عام 550 قبل الميلاد. امتدت في أوج شرفها من نهر السند في الشرق إلى شمال أفريقيا واليونان في الغرب، مما يجعلها أكبر إمبراطورية في العالم القديم.</p>
            <h3>حول تخت جمشيد (برسبوليس)</h3>
            <p>تخت جمشيد (برسبوليس)، التي تعني حرفياً "مدينة الفرس"، كانت العاصمة الاحتفالية للإمبراطورية الأخامنشية، وتقع على بعد 70 كيلومتراً شمال شرق شيراز في إيران الحديثة. بدأ بناء هذا المجمع المعماري الرائع خلال عهد داريوس الكبير وتم من قبل خلفائه خشايارشا الأول وأردشير الأول.</p>
            <h3>المنشآت الهامة</h3>
            <ul>
            <li><strong>بوابة جميع الأمم:</strong> المدخل الكبير لتخت جمشيد الذي بناه خشايارشا الأول.</li>
            <li><strong>قصر الأبادانا:</strong> القصر الاحتفالي الكبير الذي بدأه داريوس الأول، ويشتهر بالنقوش التي تصور حاملي الهدايا من جميع البلدان التابعة.</li>
            <li><strong>قصر المئة عمود:</strong> أحد أكبر القصور التي تغطي حوالي 4300 متر مربع.</li>
            <li><strong>قصر تشرا:</strong> القصر الخاص بداريوس الكبير، المعروف أيضاً بقصر المرآة.</li>
            <li><strong>قصر الهاديش:</strong> القصر الخاص بخشايارشا الأول.</li>
            </ul>
            <h3>التدمير</h3>
            <p>في عام 330 قبل الميلاد، بعد هزيمة داريوس الثالث، سار إسكندر المقدوني نحو تخت جمشيد. وبعد فترة من الاحتفالات، أُحرقت القصور. لا تزال الأسباب الدقيقة وراء إحراق تخت جمشيد موضع نقاش بين المؤرخين حتى يومنا هذا.</p>
            """;

        var faContent2 = """
            <h2>کوروش دوم (کوروش بزرگ)</h2>
            <p>کوروش دوم یا کوروش بزرگ (۵۷۶ – ۵۳۰ پیش از میلاد)، بنیان‌گذار امپراتوری هخامنشی و یکی از چهره‌های ماندگار در تاریخ جهان است. او با پایان دادن به سلطه مادها و تشکیل اولین امپراتوری چندملیتی جهان، گامی بلند در تاریخ تمدن برداشت.</p>
            <h3>زندگی کوروش</h3>
            <p>بر اساس تاریخ‌نگاری هرودوت، کوروش پسربچه آستیاگ پادشاه ماد بود. پدرش کامبوج و مادرش ماندانه دختر آستیاگ نام داشتند. کوروش در سال ۵۵۹ پیش از میلاد بر تخت پادشاهی پارس نشست و در ۵۵۰ پیش از میلاد، مادها را فتح کرد و بنیان حکومت هخامنشیان را نهاد.</p>
            <h3>فتوحات نظامی</h3>
            <ul>
            <li><strong>فتح لیدیه (۵۴۶ پیش از میلاد):</strong> پیروزی بر کرویزوس پادشاه لیدیه و گسترش مرزها تا سواحل مدیترانه.</li>
            <li><strong>فتح بابِل (۵۳۹ پیش از میلاد):</strong> تصرف پایتخت پادشاهی بابِل و آزادسازی یهودیان اسیر شده.</li>
            <li><strong>تسخیر شرق:</strong> فتح سغد، باکتریا و Gandhara تا نهر سند.</li>
            </ul>
            <h3>منشور کوروش</h3>
            <p>منشور کوروش یا استوانه کوروش، استوانه‌ای از جنس پخته‌گِل است که در سال ۱۸۷۹ در بابل کشف شده و روی آن به خط میخی عکدی فرمان کوروش درباره احترام به ادیان و آزادی مردم نوشته شده است. این منشور توسط بسیاری به عنوان اولین سند حقوق بشر شناخته می‌شود.</p>
            <h3>مرگ و میراث</h3>
            <p>کوروش بزرگ در سال ۵۳۰ پیش از میلاد در یک نبرد با قبایل Massagetae در سواحل دریای آرال به شهادت رسید. آرامگاه او در پاسارگاد واقع شده و تا به امروز پابرجاست. میراث کوروش شامل احترام به تفاوت‌ها، مدارا با اقوام مختلف و حکومت عادلانه است که در تمام طول تاریخ مورد تحسین بوده است.</p>
            """;

        var enContent2 = """
            <h2>Cyrus II (Cyrus the Great)</h2>
            <p>Cyrus II of Persia, commonly known as Cyrus the Great (600–530 BCE), was the founder of the Achaemenid Empire and one of the most enduring figures in world history. By ending the domination of the Medes and creating the world's first multicultural empire, he took a monumental step in the history of civilization.</p>
            <h3>Life of Cyrus</h3>
            <p>According to Herodotus, Cyrus was the grandson of Astyages, King of the Medes. His father was Cambyses I and his mother was Mandane, daughter of Astyages. Cyrus ascended the throne of Persia in 559 BCE, and in 550 BCE conquered the Medes, laying the foundations of the Achaemenid dynasty.</p>
            <h3>Military Conquests</h3>
            <ul>
            <li><strong>Conquest of Lydia (546 BCE):</strong> Victory over King Croesus of Lydia, extending borders to the Mediterranean coast.</li>
            <li><strong>Conquest of Babylon (539 BCE):</strong> Taking the capital of the Babylonian Empire and freeing the enslaved Jewish people.</li>
            <li><strong>Eastern Campaigns:</strong> Conquest of Sogdiana, Bactria and Gandhara up to the Indus River.</li>
            </ul>
            <h3>The Cyrus Cylinder</h3>
            <p>The Cyrus Cylinder is a clay cylinder discovered in 1879 in Babylon, inscribed in Akkadian cuneiform with Cyrus's declaration regarding religious tolerance and freedom of the people. It is widely regarded as the first document of human rights.</p>
            <h3>Death and Legacy</h3>
            <p>Cyrus the Great died in 530 BCE in battle against the Massagetae tribes near the Aral Sea. His tomb is located in Pasargadae and still stands today. Cyrus's legacy of respect for diversity, tolerance of different peoples, and just governance has been admired throughout history.</p>
            """;

        var arContent2 = """
            <h2>كوروش الثاني (كوروش الكبير)</h2>
            <p>كوروش الثاني من فارس، المعروف عادةً بكوروش الكبير (600-530 قبل الميلاد)، هو مؤسس الإمبراطورية الأخامنشية وأحد الشخصيات الأكثر دواماً في تاريخ العالم. من خلال إنهاء سيطرة الميد وتأسيس أول إمبراطورية متعددة الثقافات في العالم، اتخذ خطوة هائلة في تاريخ الحضارة.</p>
            <h3>حياة كوروش</h3>
            <p>وفقاً لهيرودوت، كان كوروش حفيد أستياج، ملك الميد. كان والده كمبوجيس الأول وأمته ماندان، ابنة أستياج. تسلم كوروش عرش فارس في عام 559 قبل الميلاد، وفي عام 550 قبل الميلاد فتح الميد، مما وضع أسس الأسرة الأخامنشية.</p>
            <h3>الفتوحات العسكرية</h3>
            <ul>
            <li><strong>فتح ليديا (546 قبل الميلاد):</strong> النصر على الملك كرويسوس ملك ليديا، وامتداد الحدود إلى ساحل البحر الأبيض المتوسط.</li>
            <li><strong>فتح بابل (539 قبل الميلاد):</strong> الاستيلاء على عاصمة الإمبراطورية البابلية وتحرير الشعب اليهودي المستعبد.</li>
            <li><strong>الحملات الشرقية:</strong> فتح سغديانا وباختريا وغندارا حتى نهر السند.</li>
            </ul>
            <h3>أسطوانة كوروش</h3>
            <p>أسطوانة كوروش هي أسطوانة طينية اكتشفت عام 1879 في بابل، ومكتوبة بالخط المسماري الأكادي بإعلان كوروش بشأن التسامح الديني وحرية الشعب. وتعتبر على نطاق واسع أول وثيقة لحقوق الإنسان.</p>
            <h3>الوفاة والإرث</h3>
            <p>توفي كوروش الكبير في عام 530 قبل الميلاد في معركة ضد قبائل المساجيتي بالقرب من بحر الآرال. يقع ضريحه في باسارغادا وما زال قائماً حتى اليوم. لقد تم الإعجاب بإرث كوروش المتمثل في احترام التنوع والتسامح مع الشعوب المختلفة والحكم العادل طوال التاريخ.</p>
            """;

        var faContent3 = """
            <h2>هنر و معماری ایران</h2>
            <p>هنر و معماری ایران یکی از غنی‌ترین و تاثیرگذارترین سبک‌های هنری در تاریخ تمدن بشری به شمار می‌رود. از آجر پخته تا کاشی هفت‌رنگ، از گنبد پیوندی تا باغ‌های ایرانی؛ همگی بازتابی از فرهنگ و اندیشه ایرانیان در طول هزاران سال هستند.</p>
            <h3>دوره باستان</h3>
            <p>معماری دوره هخامنشی در تخت‌جمشید و پاسارگاد با استفاده از ستون‌های بلند، سرستون‌های جانوری و نقش‌برجسته‌های متعدد یکی از اوج‌های معماری جهان باستان است. معماری ساسانی با ساخت گنبدهای بزرگ، تالارهای ایوانی و کاخ‌های شکوهمند مثل کtesiphon تأثیر عمیقی بر معماری بعدی گذاشت.</p>
            <h3>دوره اسلامی</h3>
            <p>با ورود اسلام به ایران، معماری ایرانی با الهام از الگوهای اسلامی و تلفیق با سنت‌های بومی، شکل جدیدی به خود گرفت. بافت‌کاری آجری، کاشی‌کاری رنگی، معرق کاشی، گچ‌بری و آینه‌کاری از ویژگی‌های بارز معماری اسلامی ایرانی هستند.</p>
            <h3>عناصر کلیدی معماری ایرانی</h3>
            <ul>
            <li><strong>ایوان:</strong> تالار پوشیده‌ای که از یک سو به حیاط باز می‌شود و نماد معماری ایرانی-اسلامی است.</li>
            <li><strong>گنبد پیوندی (کاربندی):</strong> ساختار هندسی پیشرفته برای پوشش فضاهای بزرگ بدون ستون.</li>
            <li><strong>باغ ایرانی:</strong> طراحی باغ‌های چهارباغ با تقسیم متقارن و منابع آبی، الهام‌بخش باغ‌های سبک پارسی در سراسر جهان.</li>
            <li><strong>کاشی‌کاری:</strong> از کاشی موزاییک تا کاشی هفت‌رنگ، یکی از زیباترین هنرهای تزئینی ایرانی.</li>
            <li><strong>نقش‌بری روی سنگ و گچ:</strong> نقوش هندسی، گیاهی و کتیبه‌های خوشنویس شده که نقش مهمی در تزئین بناها دارند.</li>
            </ul>
            <h3>هنرهای تزئینی</h3>
            <p>هنرهایی مانند منسوجات ابریشمی، فرش بافی، صنایع مسی، سفال و ظروف زرین، خوشنویسی و مینیاتور همگی بخشی از میراث هنری غنی ایران هستند که به خوشه‌ای از فرهنگ ایرانی جهان شهرت دارند.</p>
            """;

        var enContent3 = """
            <h2>Iranian Art and Architecture</h2>
            <p>Iranian art and architecture is considered one of the richest and most influential artistic styles in the history of human civilization. From baked brick to seven-colored tiles, from squinch domes to Persian gardens—all reflect Iranian culture and thought over thousands of years.</p>
            <h3>Ancient Period</h3>
            <p>Achaemenid architecture at Persepolis and Pasargadae, with its tall columns, animal capitals, and numerous reliefs, represents one of the peaks of ancient world architecture. Sasanian architecture, with its large domes, iwan halls, and magnificent palaces like Ctesiphon, deeply influenced later architecture.</p>
            <h3>Islamic Period</h3>
            <p>With the arrival of Islam in Iran, Iranian architecture took on new forms inspired by Islamic patterns yet integrated with native traditions. Brickwork, colorful tiling, mosaic tilework, plasterwork, and mirror work are prominent features of Iranian Islamic architecture.</p>
            <h3>Key Elements of Iranian Architecture</h3>
            <ul>
            <li><strong>Iwan:</strong> A vaulted hall open on one side toward a courtyard, a hallmark of Iranian-Islamic architecture.</li>
            <li><strong>Muqarnas (Squinch):</strong> Advanced geometric structure for covering large spaces without columns.</li>
            <li><strong>Persian Garden:</strong> The Charbagh design with symmetrical divisions and water features, inspiring Persian-style gardens worldwide.</li>
            <li><strong>Tiling:</strong> From mosaic tiles to seven-colored (haft-rang) tiles, one of the most beautiful Iranian decorative arts.</li>
            <li><strong>Stone and Plaster Carving:</strong> Geometric, floral patterns and calligraphic inscriptions play a major role in building decoration.</li>
            </ul>
            <h3>Decorative Arts</h3>
            <p>Arts such as silk textiles, carpet weaving, copper crafts, pottery and goldsmithing, calligraphy, and miniature painting are all part of Iran's rich artistic heritage, celebrated as a jewel of Iranian culture worldwide.</p>
            """;

        var arContent3 = """
            <h2>الفن والعمارة الإيرانية</h2>
            <p>يعتبر الفن والعمارة الإيرانية من أغنى الأنماط الفنية وأكثرها تأثيراً في تاريخ حضارة البشرية. من الطوب المخبوز إلى البلاط ذي الألوان السبعة، ومن الأقبية إلى الحدائق الفارسية - تعكس جميعها الثقافة والفكر الإيراني على مدى آلاف السنين.</p>
            <h3>الفترة القديمة</h3>
            <p>تمثل العمارة الأخامنشية في تخت جمشيد وباسارغادا، بأعمدتها الطويلة ورؤوس الأعمدة الحيوانية والنقوش العديدة، أحد قمم عمارة العالم القديم. تركت العمارة الساسانية، بأقبيتها الكبرى وقاعات الإيوان والقصور المهيبة مثل قطسيفون، أثراً عميقاً على العمارة اللاحقة.</p>
            <h3>الفترة الإسلامية</h3>
            <p>مع وصول الإسلام إلى إيران، اتخذت العمارة الإيرانية أشكالاً جديدة مستوحاة من الأنماط الإسلامية مع دمجها مع التقاليد المحلية. يعمل الطوب، والبلاط الملون، وبلاط الفسيفساء، والتجصير، والمرايا من السمات البارزة للعمارة الإسلامية الإيرانية.</p>
            <h3>العناصر الأساسية للعمارة الإيرانية</h3>
            <ul>
            <li><strong>الإيوان:</strong> قاعة مقببة مفتوحة من جهة واحدة نحو الفناء، وهي سمة مميزة للعمارة الإسلامية الإيرانية.</li>
            <li><strong>المقرنصات:</strong> هيكل هندسي متقدم لتغطية المساحات الكبيرة بدون أعمدة.</li>
            <li><strong>الحديقة الفارسية:</strong> تصميم تشارباغ بالتقسيمات المتماثلة والميزات المائية، الذي ألهم الحدائق ذات الطراز الفارسي في جميع أنحاء العالم.</li>
            <li><strong>البلاط:</strong> من بلاط الفسيفساء إلى بلاط الألوان السبعة، وهو أحد أجمل الفنون الزخرفية الإيرانية.</li>
            <li><strong>النقش على الحجر والجص:</strong> الأنماط الهندسية والنباتية والنقوش الخطية تلعب دوراً كبيراً في زخرفة المباني.</li>
            </ul>
            <h3>الفنون الزخرفية</h3>
            <p>الفنون مثل المنسوجات الحريرية، وحصر السجاد، والحرف النحاسية، والفخار والصناعات الذهبية، والخط العربي، واللوحات المصغرة كلها جزء من الإرث الفني الغني لإيران، التي تحتفل بها كجوهرة من الثقافة الإيرانية في جميع أنحاء العالم.</p>
            """;

        var faContent4 = """
            <h2>میراث فرهنگی ایران</h2>
            <p>ایران با دارا بودن بیش از ۲۷ اثر ثبت‌شده در فهرست میراث جهانی یونسکو و صدها اثر ملی، از جمله سرزمین‌های غنی از نظر میراث فرهنگی در جهان است. این میراث شامل مجموعه‌های معماری، شهرهای تاریخی، باغ‌های ایرانی، آثار باستانی و همچنین افسانه‌ها و سنت‌های زنده است.</p>
            <h3>چند اثر برجسته میراث جهانی ایران</h3>
            <ul>
            <li><strong>تخت‌جمشید (۱۹۷۹):</strong> پایتخت رسمی امپراتوری هخامنشی و نمادی از شکوه باستان ایران.</li>
            <li><strong>نقش رستم (۱۹۷۹):</strong> آرامگاه‌های ساسانی و نقش‌های تاریخی در نزدیکی شیراز.</li>
            <li><strong>مسجد جامع اصفهان (۱۹۷۹):</strong> شاهکاری از معماری دوره سلجوقی و ایلخانی با چهار ایوان.</li>
            <li><strong>میدان شاه/امام خمینی اصفهان (۱۹۷۹):</strong> میدان بزرگ شاهکار معماری دوره صفوی شامل مسجد شاه، مسجد شیخ لطف‌الله، عالی قاپو و سرای قیصریه.</li>
            <li><strong>تخت سلیمان (۲۰۰۳):</strong> آتشکده و محوطه زرتشتی دوره ساسانی در آذربایجان غربی.</li>
            <li><strong>بام و شهر باستانی آن (۲۰۰۴):</strong> بزرگ‌ترین ساختمان آجری خشتی جهان که بعد از زلزله ۱۳۸۲ بازسازی شد.</li>
            <li><strong>پاسارگاد (۲۰۰۴):</strong> اولین پایتخت هخامنشی و آرامگاه کوروش بزرگ.</li>
            <li><strong>شهر تاریخی یزد (۲۰۱۷):</strong> نمونه بافت عاشقانه معماری خشتی، آتشکده زرتشتی و کویری.</li>
            <li><strong>باغ‌های ایرانی (۲۰۱۱):</strong> ۹ باغ تاریخی در استان‌های مختلف ایران به عنوان الگوی طراحی باغ چارباغ.</li>
            <li><strong>ترنج و کاریزهای اصفهان (۲۰۲۴):</strong> جدیدترین ثبت شده در فهرست یونسکو.</li>
            </ul>
            <h3>میراث ناملموس</h3>
            <p>علاوه بر آثار فیزیکی، میراث ناملموس ایران شامل موسیقی سنتی، شعر و ادبیات، دستبافت فرش، سنت‌های آشپزی، جشن‌های میدانی نوروز، چاه‌های کاریزی، تئاتر تعزیه و هنرهای سایه‌بازی است که بخشی از هویت ملی ایرانیان می‌باشند.</p>
            """;

        var enContent4 = """
            <h2>Cultural Heritage of Iran</h2>
            <p>With more than 27 sites inscribed on the UNESCO World Heritage List and hundreds of national heritage sites, Iran is one of the world's richest countries in cultural heritage. This legacy includes architectural complexes, historic cities, Persian gardens, ancient monuments, as well as living legends and traditions.</p>
            <h3>Some Outstanding World Heritage Sites of Iran</h3>
            <ul>
            <li><strong>Persepolis (1979):</strong> Ceremonial capital of the Achaemenid Empire and a symbol of ancient Iranian glory.</li>
            <li><strong>Naqsh-e Rostam (1979):</strong> Sasanian rock tombs and historical reliefs near Shiraz.</li>
            <li><strong>Jameh Mosque of Isfahan (1979):</strong> A masterpiece of Seljuk and Ilkhanid architecture with its four-iwan plan.</li>
            <li><strong>Meidan Emam (Naqsh-e Jahan), Isfahan (1979):</strong> The grand Safavid square, a masterpiece featuring the Shah Mosque, Sheikh Lotfollah Mosque, Ali Qapu palace, and Qeysarieh Bazaar.</li>
            <li><strong>Takht-e Soleyman (2003):</strong> Zoroastrian fire temple and Sasanian complex in West Azerbaijan.</li>
            <li><strong>Bam and its Cultural Landscape (2004):</strong> World's largest adobe-brick structure, rebuilt after the devastating 2003 earthquake.</li>
            <li><strong>Pasargadae (2004):</strong> First capital of the Achaemenids and tomb of Cyrus the Great.</li>
            <li><strong>Historic City of Yazd (2017):</strong> A romantic fabric of adobe architecture, Zoroastrian fire temples, and desert cityscapes.</li>
            <li><strong>The Persian Garden (2011):</strong> 9 historic gardens across Iranian provinces as paradigms of Charbagh design.</li>
            <li><strong>Tureng (Hares) and Qanats of Isfahan (2024):</strong> The most recent addition to Iran's UNESCO list.</li>
            </ul>
            <h3>Intangible Cultural Heritage</h3>
            <p>Beyond physical sites, Iran's intangible heritage includes traditional music, poetry and literature, hand-woven carpets, culinary traditions, Nowruz (Persian New Year) celebrations, qanat irrigation systems, Ta'zieh theatre, and shadow-puppet arts—all part of the Iranian national identity.</p>
            """;

        var arContent4 = """
            <h2>التراث الثقافي الإيراني</h2>
            <p>مع وجود أكثر من 27 موقعاً مسجلاً في قائمة التراث العالمي لليونسكو ومئات المواقع التراثية الوطنية، تعد إيران من أغنى البلدان في العالم بالتراث الثقافي. يشمل هذا الإرث المجمعات المعمارية والمدن التاريخية والحدائق الفارسية والآثار القديمة، فضلاً عن الأساطير والتقاليد الحية.</p>
            <h3>بعض المواقع التراثية العالمية البارزة في إيران</h3>
            <ul>
            <li><strong>برسبوليس (تخت جمشيد) (1979):</strong> العاصمة الاحتفالية للإمبراطورية الأخامنشية ورمز مجد إيران القديم.</li>
            <li><strong>نقش رستم (1979):</strong> مقابر صخرية ساسانية ونقوش تاريخية بالقرب من شيراز.</li>
            <li><strong>جامع عصقفان (1979):</strong> تحفة فنية للعمارة السلجوقية والإلخانية بتصميمها المكون من أربعة أروقة.</li>
            <li><strong>ميدان إمام (نقش جهان) بأصبهان (1979):</strong> الساحة الصفوية الكبرى، وهي تحفة تتضمن مسجد الشاه ومسجد الشيخ لطف الله وقصر علي قابو وبازار قيصرية.</li>
            <li><strong>تخت سليمان (2003):</strong> معبد نار زرتشختي ومجمع ساساني في أذربيجان الغربية.</li>
            <li><strong>بام ومشهدها الثقافي (2004):</strong> أكبر بنية طينية من الطوب اللبن في العالم، وأعيد بناؤها بعد زلزال عام 2003 المدمر.</li>
            <li><strong>باسارغادا (2004):</strong> أول عاصمة للأخامنشية وضريح كوروش الكبير.</li>
            <li><strong>المدينة التاريخية يزد (2017):</strong> نسيج رومانسي للعمارة الطينية ومعابد النار الزرتشتية ومناظر مدينة صحراوية.</li>
            <li><strong>الحديقة الفارسية (2011):</strong> 9 حدائق تاريخية في مختلف المقاطعات الإيرانية كنماذج لتصميم تشارباغ.</li>
            <li><strong>تورنج وقنوات أصبهان (2024):</strong> أحدث إضافة إلى قائمة اليونسكو لإيران.</li>
            </ul>
            <h3>التراث الثقافي غير المادي</h3>
            <p>إلى جانب المواقع المادية، يشمل التراث غير المادي لإيران الموسيقى التقليدية والشعر والأدب والسجاد المنسوج يدوياً والتقاليد الطهوية واحتفالات نوروز (العيد الفارسي الجديد) وأنظمة الري بالقنوات والمسرحية التعزية وفنون الدمى الظلية - وكلها جزء من الهوية الوطنية الإيرانية.</p>
            """;

        var faContent5 = """
            <h2>زبان فارسی و ادبیات آن</h2>
            <p>زبان فارسی یکی از کهن‌ترین و تأثیرگذارترین زبان‌های هند و اروپایی است که در طی بیش از دو هزار و پانصد سال تداوم یافته است. این زبان در ایران، افغانستان، تاجیکستان و بخش‌هایی از ازبکستان، هند و پاکستان زبان رسمی یا فرهنگی محسوب می‌شود.</p>
            <h3>سیر تاریخی زبان فارسی</h3>
            <ul>
            <li><strong>فارسی باستان:</strong> زبان رسمی امپراتوری هخامنشی که با خط میخی بر روی نقش‌ها و منشورها ثبت شده است.</li>
            <li><strong>فارسی میانه:</strong> زبان ادبی و رسمی دوران اشکانیان و ساسانیان که به خط پهلوی می‌نوشتند.</li>
            <li><strong>فارسی نو:</strong> بعد از اسلام و آمیختگی با واژگان عربی، شکل نوین فارسی پدید آمد که از حدود قرن سوم هجری تا به امروز ادامه داشته است.</li>
            </ul>
            <h3>شاعران بزرگ ادبیات فارسی</h3>
            <p>ادبیات فارسی میهن ما را می‌توان به سه دوره اصلی تقسیم کرد:</p>
            <ul>
            <li><strong>دوره قرن‌های میانی (قرون ۴ تا ۸ هجری):</strong> رودکی، فردوسی (شاهنامه)، خدوی خان، ناصر خسرو، مولوی جلال‌الدین بلخی، سعدی (گلستان و بوستان)، حافظ شیرازی، عمر خیام (رباعیات)، نظامی گنجوی (پنج گنج).</li>
            <li><strong>دوره بازگشت ادبی (قرن ۱۳ هجری):</strong> مظفر کاظمی، مهرزاد اصفهانی، احمد رزاز، عارف قزوینی، پیروز وثوقی.</li>
            <li><strong>شعر نو:</strong> نیما یوشیج بنیانگذار شعر نو فارسی بود و پس از او سهراب سپهری، فروغ فرخزاد، احمد شاملو، مهدی اخوان ثالث و ... شعر مدرن ایرانی را شکل دادند.</li>
            </ul>
            <h3>ادبیات نثر فارسی</h3>
            <p>علاوه بر شعر، ادبیات نثر فارسی نیز سنت غنی دارد. کتبی مانند «شاهنامه» فردوسی، «مرزبان‌نامه»، «کلیله و دمنه»، «قابوس‌نامه» و در قرون اخیر «بوف کور» سادات حسین تبریزی، «شاهکارها» محمد عالی، و رمان‌های هوشنگ گلشیری، محمود دولت‌آبادی، غلامحسین ساعدی از جلوه‌های برجسته این سنت هستند.</p>
            """;

        var enContent5 = """
            <h2>Persian Language and Literature</h2>
            <p>Persian (Farsi) is one of the oldest and most influential Indo-European languages, with a continuous documented history of more than 2,500 years. It is the official or cultural language of Iran, Afghanistan, Tajikistan, and parts of Uzbekistan, India, and Pakistan.</p>
            <h3>Historical Evolution of Persian</h3>
            <ul>
            <li><strong>Old Persian:</strong> The official language of the Achaemenid Empire, inscribed in cuneiform on reliefs and cylinders.</li>
            <li><strong>Middle Persian (Pahlavi):</strong> The literary and official language of the Arsacid and Sasanian eras, written in the Pahlavi script.</li>
            <li><strong>New Persian:</strong> After the Islamic conquest and absorption of Arabic vocabulary, modern Persian emerged around the 9th century CE, continuing with remarkable continuity to the present day.</li>
            </ul>
            <h3>Great Poets of Persian Literature</h3>
            <p>Persian literature can be broadly divided into three major periods:</p>
            <ul>
            <li><strong>Medieval Golden Age (10th–15th c.):</strong> Rudaki, Ferdowsi (Shahnameh), Khayyam, Naser Khosrow, Rumi (Jalal ad-Din Muhammad Balkhi), Saadi Shirazi (Gulistan and Bustan), Hafez Shirazi, Omar Khayyam (Rubaiyat), Nizami Ganjavi (Khamsa).</li>
            <li><strong>Return Literary Period (19th c.):</strong> Muzaffar Kazemi, Mehrezad Esfahani, Ahmad Razaz, Aref Qazvini, Piruz Vosoughi.</li>
            <li><strong>Modern Poetry (She'r-e No):</strong> Nima Yooshij was the founder of modern Persian poetry, followed by Sohrab Sepehri, Forugh Farrokhzad, Ahmad Shamlou, Mehdi Akhavan-Sales and others who shaped contemporary Iranian poetry.</li>
            </ul>
            <h3>Persian Prose</h3>
            <p>Beyond poetry, Persian prose has a rich tradition. Works like Ferdowsi's "Shahnameh," "Marzban-nama," "Kalilah wa Dimnah," "Qabus-nama," and more recently "The Blind Owl" by Sadegh Hedayat, "Shahkar-ha" by Mohammad Ali, and novels by Houshang Golshiri, Mahmoud Dowlatabadi, and Gholam-Hossein Sa'edi are outstanding representatives of this tradition.</p>
            """;

        var arContent5 = """
            <h2>اللغة الفارسية وأدبها</h2>
            <p>اللغة الفارسية (الفارسية) هي واحدة من أقدم اللغات الهندو أوروبية وأكثرها تأثيراً، مع تاريخ مستمر موثق لأكثر من 2500 عام. إنها اللغة الرسمية أو الثقافية لإيران وأفغانستان وطاجيكستان وأجزاء من أوزبكستان والهند وباكستان.</p>
            <h3>التطور التاريخي للغة الفارسية</h3>
            <ul>
            <li><strong>الفارسية القديمة:</strong> اللغة الرسمية للإمبراطورية الأخامنشية، المكتوبة بالخط المسماري على النقوش والأسطوانات.</li>
            <li><strong>الفارسية الوسطى (البهلوية):</strong> اللغة الأدبية والرسمية لعهد الأشكانيين والساسانيين، المكتوبة بخط البهلوية.</li>
            <li><strong>الفارسية الحديثة:</strong> بعد الفتح الإسلامي واستيعاب المفردات العربية، نشأت الفارسية الحديثة حوالي القرن التاسع الميلادي، واستمرت باستمرار ملحوظ حتى يومنا هذا.</li>
            </ul>
            <h3>شعراء كبار في الأدب الفارسي</h3>
            <p>يمكن تقسيم الأدب الفارسي بشكل عام إلى ثلاث فترات رئيسية:</p>
            <ul>
            <li><strong>العصر الذهبي الوسيط (العاشر - الخامس عشر م.):</strong> روداكي، فردوسي (شاهنامه)، الخيام، ناصر خسرو، جلال الدين الرومي البلخي، سعدي الشيرازي (غولستان وبوستان)، حافظ الشيرازي، عمر الخيام (الرباعيات)، نظامي گنجوي (الخمسة).</li>
            <li><strong>الفترة الأدبية للعودة (التاسع عشر م.):</strong> مظفر كاظمي، مهرزاد أصفهاني، أحمد رزاز، عارف قزويني، بيروز فوسوغي.</li>
            <li><strong>الشعر الحديث (شعر نو):</strong> كان نيما يوشيج مؤسس الشعر الفارسي الحديث، تبعه سهراب سيبهري، وفروغ فروخزاد، وأحمد شاملو، ومهدي أخوان ثالث وغيرهم ممن شكلوا الشعر الإيراني المعاصر.</li>
            </ul>
            <h3>النثر الفارسي</h3>
            <p>إلى جانب الشعر، يتمتع النثر الفارسي بتقليد غني. أعمال مثل "شاهنامه" فردوسي، و"مرزبان نامه"، و"كليلة ودمنة"، و"قابوس نامه"، وفي الآونة الأخيرة "البومة العمياء" لصادق هدايت، و"شاهكارها" لمحمد علي، والروايات لهوشنگ گلشيري، ومحمود دولت آبادي، وغلام حسين ساعدي هم من الممثلين البارزين لهذا التقليد.</p>
            """;

        var faContent6 = """
            <h2>موزه‌ها و باستان‌شناسی در ایران</h2>
            <p>ایران با داشتن تمدنی چند هزار ساله، به عنوان یکی از مهم‌ترین حوزه‌های تحقیقات باستان‌شناسی در جهان محسوب می‌شود. سالانه ده‌ها کاوش باستان‌شناسی در نقاط مختلف کشور انجام می‌شود و هزاران اثر به مجموعه‌های موزه‌ای ایران و سراسر جهان اضافه می‌گردد.</p>
            <h3>موزه‌های مهم ایران</h3>
            <ul>
            <li><strong>موزه ایران باستان (تهران):</strong> بزرگ‌ترین و مهم‌ترین موزه تاریخ و باستان‌شناسی ایران است که در آن آثار ارزشمندی از تمدن‌های سیلک، جیرفت، هخامنشی، ساسانی و دوره‌های بعدی به نمایش گذاشته می‌شود.</li>
            <li><strong>موزه جمهوری اسلامی ایران (تهران):</strong> در کنار موزه ایران باستان قرار دارد و عمدتاً به نگهداری آثار دوره اسلامی ایران اختصاص یافته است.</li>
            <li><strong>موزه‌های شیراز (نارنجستان قوام، برج ارگ کریمخانی، پرسپولیس):</strong> مجموعه‌ای از موزه‌ها در شیراز که هر کدام به دوره یا موضوع خاصی اختصاص دارند.</li>
            <li><strong>موزه‌های اصفهان:</strong> شامل موزه چهلستون، موزه تیموریه، موزه قصر، که نمونه‌هایی از هنر و معماری صفوی و بعدی را در خود جای داده‌اند.</li>
            <li><strong>موزه‌های یزد (عالم بادگیر، باغ دولت‌آباد):</strong> با تمرکز بر معماری کویری و آداب و رسوم مردم منطقۀ یزد.</li>
            <li><strong>موزه جیرفت (کرمان):</strong> شامل آثار تازه پیدا شده از تمدن هالیل رود که یکی از قدیمی‌ترین تمدن‌های جهان است.</li>
            </ul>
            <h3>کاوش‌های مهم باستان‌شناسی</h3>
            <ul>
            <li><strong>کاوش‌های تخت‌جمشید و پاسارگاد:</strong> توسط تیم‌های فرانسوی، آمریکایی و ایرانی در طول قرن نوزدهم و بیستم انجام شد.</li>
            <li><strong>کاوش‌های شوش (شوشیان):</strong> توسط تیم فرانسوی ژاک دو مورگان که منجر به کشف کدهای حمورابی و سایر هفت هزار اثر ارزشمند شد.</li>
            <li><strong>کاوش‌های کویر دشتی مروان و جیرفت:</strong> در سال‌های اخیر و کشف تمدن هالیل رود که مربوط به ۵۰۰۰ سال قبل است.</li>
            <li><strong>کاوش‌های سیلک و سراخته و حسنلو:</strong> در کرج، زنجان و آذربایجان غربی برای روشن ساختن دوران آهن و برنز ایران.</li>
            </ul>
            <h3>نقش باستان‌شناسی در ترویج میراث</h3>
            <p>باستان‌شناسی در ایران نه تنها به درک تاریخ گذشته تمدن ایرانی کمک می‌کند، بلکه مبنایی برای برنامه‌های حفاظت از میراث فرهنگی، توسعه گردشگری فرهنگی، و تحقیقات علمی در سطح بین‌المللی فراهم می‌آورد.</p>
            """;

        var enContent6 = """
            <h2>Museums and Archaeology in Iran</h2>
            <p>With a multi-millennial civilization, Iran stands as one of the most important fields of archaeological research in the world. Each year, dozens of archaeological excavations are conducted across the country, and thousands of artifacts are added to Iranian and global museum collections.</p>
            <h3>Major Museums of Iran</h3>
            <ul>
            <li><strong>National Museum of Iran (Tehran):</strong> The largest and most important museum of Iranian history and archaeology, showcasing invaluable artifacts from civilizations of Sialk, Jiroft, Achaemenid, Sasanian and later periods.</li>
            <li><strong>Museum of the Islamic Era (Tehran):</strong> Adjacent to the National Museum of Iran, dedicated mainly to the preservation of artifacts from Iran's Islamic period.</li>
            <li><strong>Museums of Shiraz (Naranjestan-e Qavam, Arg of Karim Khan, Persepolis Museum):</strong> A group of museums in Shiraz, each dedicated to a specific period or subject.</li>
            <li><strong>Museums of Isfahan:</strong> Including Chehel Sotoun Museum, Timurid Museum, and Qasr Museum, housing fine examples of Safavid and post-Safavid art and architecture.</li>
            <li><strong>Museums of Yazd (World of Windcatchers, Dowlatabad Garden Museum):</strong> Focused on desert architecture and local traditions of the Yazd region.</li>
            <li><strong>Jiroft Museum (Kerman):</strong> Showcasing newly discovered artifacts from the Halil Rud Civilization, one of the world's oldest.</li>
            </ul>
            <h3>Major Archaeological Excavations</h3>
            <ul>
            <li><strong>Excavations at Persepolis and Pasargadae:</strong> Conducted by French, American, and Iranian teams throughout the 19th and 20th centuries.</li>
            <li><strong>Excavations at Susa:</strong> By the French team of Jacques de Morgan, leading to the discovery of the Code of Hammurabi and 7,000 other valuable artifacts.</li>
            <li><strong>Excavations at Dasht-e Marv and Jiroft:</strong> In recent years, uncovering the Halil Rud Civilization dating back 5,000 years.</li>
            <li><strong>Excavations at Sialk, Teppe Saryazd, and Hasanlu:</strong> In Karaj, Zanjan, and West Azerbaijan, illuminating Iran's Bronze and Iron Ages.</li>
            </ul>
            <h3>The Role of Archaeology in Heritage Promotion</h3>
            <p>Archaeology in Iran not only helps understand the past of Iranian civilization but also provides a foundation for cultural heritage conservation programs, cultural tourism development, and scientific research at the international level.</p>
            """;

        var arContent6 = """
            <h2>المتاحف وعلم الآثار في إيران</h2>
            <p>مع وجود حضارة تعود لعدة ألاف السنين، تحتل إيران مكانة كواحدة من أهم مجالات البحث الأثري في العالم. وتجري كل عشرات الحفريات الأثرية في جميع أنحاء البلاد، وتضاف آلاف القطع الأثرية إلى المجموعات المتحفية الإيرانية والعالمية.</p>
            <h3>أهم المتاحف في إيران</h3>
            <ul>
            <li><strong>المتحف الوطني الإيراني (طهران):</strong> هو أكبر وأهم متحف للتاريخ والآثار الإيرانية، ويعرض قطعاً لا تُقدر بثمن من حضارات سيالك وجيرفت والأخامنشية والساسانية والفترات اللاحقة.</li>
            <li><strong>متحف العصر الإسلامي (طهران):</strong> يقع بجانب المتحف الوطني الإيراني، ويُخصص بشكل أساسي للحفاظ على القطع من العصر الإسلامي في إيران.</li>
            <li><strong>متاحف شيراز (نارانجستان قوام، قلعة كريم خان، متحف برسبوليس):</strong> مجموعة من المتاحف في شيراز، كل منها مخصص لفترة أو موضوع معين.</li>
            <li><strong>متاحف أصبهان:</strong> بما في ذلك متحف چهل ستون ومتحف التيمورية ومتحف قصر، والتي تضم أمثلة رائعة للفن والعمارة الصفوية واللاحقة للصفويين.</li>
            <li><strong>متاحف يزد (عالم الملاقط الحوائط، متحف حديقة دولت آباد):</strong> تركز على العمارة الصحراوية والتقاليد المحلية لمنطقة يزد.</li>
            <li><strong>متحف جيرفت (كرمان):</strong> يعرض القطع المكتشفة حديثاً من حضارة هليل رود، وهي واحدة من أقدم الحضارات في العالم.</li>
            </ul>
            <h3>الحفريات الأثرية الكبرى</h3>
            <ul>
            <li><strong>الحفريات في برسبوليس وباسارغادا:</strong> أجرتها فرق فرنسية وأمريكية وإيرانية طوال القرنين التاسع عشر والعشرين.</li>
            <li><strong>الحفريات في سوزة:</strong> بواسطة الفريق الفرنسي لجاques دي مورغان، مما أدى إلى اكتشاف قانون حمورابي و7000 قطعة ثمينة أخرى.</li>
            <li><strong>الحفريات في دشت مروان وجيرفت:</strong> في السنوات الأخيرة، كشفت عن حضارة هليل رود التي يعود تاريخها إلى 5000 سنة.</li>
            <li><strong>الحفريات في سيالك وتيپ ساريازد وحسنلو:</strong> في كارج وزنجان وأذربيجان الغربية، لتوضيح عصور البرونز والحديد في إيران.</li>
            </ul>
            <h3>دور علم الآثار في الترويج للتراث</h3>
            <p>لا يساعد علم الآثار في إيران على فهم ماضي الحضارة الإيرانية فحسب، بل يوفر أيضاً الأساس لبرامج حفظ التراث الثقافي وتطوير السياحة الثقافية والبحث العلمي على المستوى الدولي.</p>
            """;

        var seedPosts = new (
            string FaTitle, string FaSlug, string FaDesc, string FaContent,
            string EnTitle, string EnSlug, string EnDesc, string EnContent,
            string ArTitle, string ArSlug, string ArDesc, string ArContent)[]
        {
            (
                "هخامنشیان و تخت‌جمشید",
                "hakhmaneshyian-o-takht-jamshid",
                "بررسی امپراتوری هخامنشی و بناهای بی‌نظیر تخت‌جمشید",
                faContent1,
                "Achaemenids and Persepolis",
                "achaemenids-and-persepolis",
                "Exploring the Achaemenid Empire and the magnificent structures of Persepolis",
                enContent1,
                "الأخامنشيون وتخت جمشيد",
                "al-akhamanashyun-wa-takht-jamshid",
                "استكشاف الإمبراطورية الأخامنشية وهياكل تخت جمشيد الرائعة",
                arContent1
            ),
            (
                "کوروش بزرگ",
                "kourosh-e-bozorg",
                "زندگی و میراث کوروش بزرگ، بنیان‌گذار امپراتوری هخامنشی",
                faContent2,
                "Cyrus the Great",
                "cyrus-the-great",
                "The life and legacy of Cyrus the Great, founder of the Achaemenid Empire",
                enContent2,
                "كوروش الكبير",
                "kurush-al-kabir",
                "حياة وإرث كوروش الكبير، مؤسس الإمبراطورية الأخامنشية",
                arContent2
            ),
            (
                "هنر و معماری",
                "honar-o-me'mari",
                "هنر و معماری ایران باستان و اسلامی",
                faContent3,
                "Art and Architecture",
                "art-and-architecture",
                "Ancient and Islamic Iranian art and architecture",
                enContent3,
                "الفن والعمارة",
                "al-fun-wal-imara",
                "الفن والعمارة الإيرانية القديمة والإسلامية",
                arContent3
            ),
            (
                "میراث فرهنگی",
                "miras-e-farhangi",
                "میراث فرهنگی ایران و ثبت جهانی آن",
                faContent4,
                "Cultural Heritage",
                "cultural-heritage",
                "Iranian cultural heritage and its worldwide registrations",
                enContent4,
                "التراث الثقافي",
                "al-turath-al-thaqafi",
                "التراث الثقافي الإيراني وتسجيلاته العالمية",
                arContent4
            ),
            (
                "زبان و ادبیات",
                "zaban-o-adabiat",
                "زبان فارسی و ادبیات غنی ایران",
                faContent5,
                "Language and Literature",
                "language-and-literature",
                "Persian language and the rich literature of Iran",
                enContent5,
                "اللغة والأدب",
                "al-lugha-wal-adab",
                "اللغة الفارسية والأدب الغني لإيران",
                arContent5
            ),
            (
                "موزه و باستان‌شناسی",
                "muzeh-o-bastan-shenasi",
                "موزه‌های ایران و دستاوردهای باستان‌شناسی",
                faContent6,
                "Museum and Archaeology",
                "museum-and-archaeology",
                "Iranian museums and archaeological achievements",
                enContent6,
                "المتحف وعلم الآثار",
                "al-mutahaf-wa-ilm-al-athar",
                "متاحف إيران والإنجازات الأثرية",
                arContent6
            ),
        };

        var existingTranslations = await db.BlogPostTranslations
            .Include(t => t.Post)
            .Where(t => t.LanguagePrefix == "fa")
            .ToDictionaryAsync(t => t.Slug, t => t, cancellationToken);

        foreach (var post in seedPosts)
        {
            Guid postId;
            if (existingTranslations.TryGetValue(post.FaSlug, out var existingFaTr))
            {
                postId = existingFaTr.PostId;

                var faTr = await db.BlogPostTranslations
                    .FirstOrDefaultAsync(t => t.PostId == postId && t.LanguagePrefix == "fa", cancellationToken);
                var enTr = await db.BlogPostTranslations
                    .FirstOrDefaultAsync(t => t.PostId == postId && t.LanguagePrefix == "en", cancellationToken);
                var arTr = await db.BlogPostTranslations
                    .FirstOrDefaultAsync(t => t.PostId == postId && t.LanguagePrefix == "ar", cancellationToken);

                if (faTr is not null && (faTr.Content is null || faTr.Content.Length < 50))
                {
                    faTr.Update(post.FaTitle, post.FaSlug, post.FaTitle, post.FaDesc, post.FaContent, post.FaTitle, post.FaDesc);
                }
                if (enTr is not null && (enTr.Content is null || enTr.Content.Length < 50))
                {
                    enTr.Update(post.EnTitle, post.EnSlug, post.EnTitle, post.EnDesc, post.EnContent, post.EnTitle, post.EnDesc);
                }
                if (arTr is not null && (arTr.Content is null || arTr.Content.Length < 50))
                {
                    arTr.Update(post.ArTitle, post.ArSlug, post.ArTitle, post.ArDesc, post.ArContent, post.ArTitle, post.ArDesc);
                }

                var existingPost = existingFaTr.Post;
                if (existingPost is not null && (existingPost.Content is null || existingPost.Content.Length < 50))
                {
                    existingPost.Update(
                        post.FaTitle, post.FaSlug, post.FaTitle, post.FaDesc, post.FaContent,
                        post.FaTitle, post.FaDesc, BlogPostStatus.Published, true);
                }

                await db.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Updated content for existing post: {FaTitle} ({EnTitle})", post.FaTitle, post.EnTitle);
                continue;
            }

            var blogPost = BlogPost.Create(
                title: post.FaTitle,
                slug: post.FaSlug,
                keyword: post.FaTitle,
                description: post.FaDesc,
                content: post.FaContent,
                metaTitle: post.FaTitle,
                metaDescription: post.FaDesc,
                status: BlogPostStatus.Published,
                commentsEnabled: true);
            db.BlogPosts.Add(blogPost);
            await db.SaveChangesAsync(cancellationToken);
            postId = blogPost.Id;

            db.BlogPostTranslations.Add(BlogPostTranslation.Create(
                postId, "fa",
                post.FaTitle, post.FaSlug,
                post.FaTitle, post.FaDesc, post.FaContent, post.FaTitle, post.FaDesc));
            db.BlogPostTranslations.Add(BlogPostTranslation.Create(
                postId, "en",
                post.EnTitle, post.EnSlug,
                post.EnTitle, post.EnDesc, post.EnContent, post.EnTitle, post.EnDesc));
            db.BlogPostTranslations.Add(BlogPostTranslation.Create(
                postId, "ar",
                post.ArTitle, post.ArSlug,
                post.ArTitle, post.ArDesc, post.ArContent, post.ArTitle, post.ArDesc));

            db.BlogPostGroups.Add(BlogPostGroup.Create(postId, groupId));
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded blog post with content: {FaTitle} ({EnTitle})", post.FaTitle, post.EnTitle);
        }
    }

    private static async Task SeedPoliticsAndVisionBlogGroupAsync(
        ContentModulesDbContext db,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var groupSlugFa = "siasat-o-cheshmandaz";
        var group = await db.BlogGroups
            .Include(g => g.Translations)
            .FirstOrDefaultAsync(g => g.Slug == groupSlugFa, cancellationToken);

        Guid groupId;
        if (group is null)
        {
            group = BlogGroup.Create(
                title: "سیاست و چشم‌انداز",
                slug: groupSlugFa,
                keyword: "سیاست, جامعه, حکمرانی, چشم‌انداز, توسعه ملی",
                description: "مقالات سیاست، جامعه و چشم‌انداز توسعه ایران",
                parentId: null);
            db.BlogGroups.Add(group);
            await db.SaveChangesAsync(cancellationToken);
            groupId = group.Id;

            db.BlogGroupTranslations.Add(BlogGroupTranslation.Create(
                groupId, "fa", "سیاست و چشم‌انداز", groupSlugFa,
                "سیاست, جامعه, حکمرانی, چشم‌انداز, توسعه ملی", "مقالات سیاست، جامعه و چشم‌انداز توسعه ایران"));
            db.BlogGroupTranslations.Add(BlogGroupTranslation.Create(
                groupId, "en", "POLITICS & VISION", "politics-and-vision",
                "politics, society, governance, vision, national development", "Articles on politics, society and Iran's development vision"));
            db.BlogGroupTranslations.Add(BlogGroupTranslation.Create(
                groupId, "ar", "السياسة والرؤية", "al-siyasah-wal-ruyah",
                "سياسة, مجتمع, حوكمة, رؤية, تنمية وطنية", "مقالات عن السياسة والمجتمع ورؤية التنمية الإيرانية"));
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded blog group: Politics & Vision (سیاست و چشم‌انداز).");
        }
        else
        {
            groupId = group.Id;
        }

        var faContentSiasat = """
            <h2>سیاست ایران در عصر جدید</h2>
            <p>سیاست خارجی و داخلی ایران در قرن بیست و یکم با چالش‌ها و فرصت‌های متعددی روبرو بوده است. از یک سو تحولات منطقه‌ای و بین‌المللی و از سوی دیگر نیازهای داخلی کشور، سیاست‌گذاران را به بازاندیشی در اولویت‌ها و سیاست‌ها واداشته است.</p>
            <h3>اصول سیاست خارجی</h3>
            <ul>
            <li><strong>استقلال ملی:</strong> حفظ تمامیت ارضی و استقلال سیاسی به عنوان اصلی تلغی‌ناپذیر.</li>
            <li><strong>عدالت بین‌الملل:</strong> حمایت از حقوق ملت‌ها و مقابله با استکبار جهانی.</li>
            <li><strong>همسایگی محور:</strong> اولویت با توسعه روابط با کشورهای همسایه و منطقه.</li>
            <li><strong>دیپلماسی چندجانبه:</strong> فعال در سازمان‌های بین‌المللی و انجمن‌های منطقه‌ای.</li>
            </ul>
            <h3>سیاست داخلی</h3>
            <p>در عرصه داخلی، تقویت نهادهای دموکراتیک، مشارکت مردمی، توسعه ظرفیت‌های علمی و فناور و ایجاد فضای سالم رقابتی در حوزه سیاسی و اقتصادی از محورهای اصلی سیاست‌گذاری در دهه اخیر بوده است. اصلاحات ساختاری در دستگاه اجرایی و ارتقای شفافیت و پاسخگویی نیز از موارد مورد تأکید قرار گرفته است.</p>
            <h3>چالش‌ها و راه‌کارها</h3>
            <p>تحریم‌های بین‌المللی، تحولات سریع تکنولوژیک، تغییرات اقلیمی و تحولات جمعیتی از مهم‌ترین چالش‌های پیش روی سیاست ایران در عصر حاضر هستند. مقابله با این چالش‌ها نیازمند رهبری مدنظرانه، برنامه‌ریزی بلندمدت و بهره‌گیری از ظرفیت‌های ملی و بین‌الملل است.</p>
            """;

        var enContentSiasat = """
            <h2>Iranian Politics in the New Era</h2>
            <p>Iran's foreign and domestic policies in the 21st century have faced numerous challenges and opportunities. On one hand, regional and international developments, and on the other, the country's internal needs, have compelled policymakers to reconsider priorities and strategies.</p>
            <h3>Principles of Foreign Policy</h3>
            <ul>
            <li><strong>National Independence:</strong> Preserving territorial integrity and political independence as an uncompromising principle.</li>
            <li><strong>International Justice:</strong> Supporting the rights of nations and opposing global hegemony.</li>
            <li><strong>Neighbor-Centered Policy:</strong> Prioritizing the development of relations with neighboring and regional countries.</li>
            <li><strong>Multilateral Diplomacy:</strong> Active participation in international organizations and regional associations.</li>
            </ul>
            <h3>Domestic Policy</h3>
            <p>On the domestic front, strengthening democratic institutions, popular participation, developing scientific and technological capacities, and creating a healthy competitive environment in the political and economic spheres have been among the main pillars of policymaking in recent decades. Structural reforms in the executive branch and promoting transparency and accountability have also been emphasized.</p>
            <h3>Challenges and Solutions</h3>
            <p>International sanctions, rapid technological change, climate change, and demographic developments are among the most important challenges facing Iran's politics today. Addressing these challenges requires farsighted leadership, long-term planning, and leveraging national and international capacities.</p>
            """;

        var arContentSiasat = """
            <h2>السياسة الإيرانية في العصر الجديد</h2>
            <p>واجهت السياسات الخارجية والداخلية لإيران في القرن الحادي والعشرين العديد من التحديات والفرص. فمن جهة التطورات الإقليمية والدولية، ومن جهة أخرى الاحتياجات الداخلية للبلاد، دفعت صانعي القرار إلى إعادة النظر في الأولويات والاستراتيجيات.</p>
            <h3>مبادئ السياسة الخارجية</h3>
            <ul>
            <li><strong>الاستقلال الوطني:</strong> الحفاظ على السلامة الإقليمية والاستقلال السياسي كمبدأ لا يقبل المساومة.</li>
            <li><strong>العدالة الدولية:</strong> دعم حقوق الشعوب ومواجهة الهيمنة العالمية.</li>
            <li><strong>السياسة المركزة على الجيران:</strong> إعطاء الأولوية لتطوير العلاقات مع الدول المجاورة والإقليمية.</li>
            <li><strong>الدبلوماسية متعددة الأطراف:</strong> المشاركة النشطة في المنظمات الدولية والجمعيات الإقليمية.</li>
            </ul>
            <h3>السياسة الداخلية</h3>
            <p>في الجبهة الداخلية، كان من بين الركائز الرئيسية لصنع السياسات في العقود الأخيرة تعزيز المؤسسات الديمقراطية، والمشاركة الشعبية، وتطوير القدرات العلمية والتكنولوجية، وخلق بيئة تنافسية صحية في المجالين السياسي والاقتصادي. كما تم التأكيد على الإصلاحات الهيكلية في الجهاز التنفيذي وتعزيز الشفافية والمساءلة.</p>
            <h3>التحديات والحلول</h3>
            <p>تعتبر العقوبات الدولية، والتغير التكنولوجي السريع، وتغير المناخ، والتطورات الديموغرافية من أهم التحديات التي تواجه السياسة الإيرانية اليوم. ومواجهة هذه التحديات تتطلب قيادة نبيرة، وتخطيطاً طويل الأمد، والاستفادة من القدرات الوطنية والدولية.</p>
            """;

        var faContentJameeh = """
            <h2>جامعه ایران و تحولات اجتماعی</h2>
            <p>جامعه ایران در قرن حاضر دستخوش تحولات عمیق اجتماعی، اقتصادی و فرهنگی شده است. رشد شهرنشینی، توسعه تحصیلات دانشگاهی، افزایش دسترسی به فناوری اطلاعات و تغییرات الگوهای خانواده از مهم‌ترین مؤلفه‌های این تحولات به شمار می‌روند.</p>
            <h3>ساختار جمعیتی</h3>
            <ul>
            <li><strong>جمعیت جوان:</strong> بیش از ۶۰ درصد جمعیت ایران زیر ۳۰ سال هستند که فرصت طلایی برای توسعه بشری محسوب می‌شود.</li>
            <li><strong>شهرنشینی:</strong> بیش از ۷۵ درصد جمعیت در شهرها زندگی می‌کنند که نیازمند برنامه‌ریزی شهری مناسب است.</li>
            <li><strong>سطح سواد:</strong> نرخ سواد بالای ۹۷ درصد و رشد چشمگیر فارغ‌التحصیلان دانشگاهی.</li>
            <li><strong>پیر شدن جمعیت:</strong> در دهه‌های آینده با چالش پیر شدن جمعیت روبرو خواهیم بود.</li>
            </ul>
            <h3>نهادهای اجتماعی</h3>
            <p>خانواده، آموزش و پرورش، دانشگاه، رسانه‌ها و نهادهای مدنی نقش تعیین‌کننده‌ای در شکل‌گیری هویت اجتماعی جوانان دارند. تقویت نهاد خانواده، ارتقای کیفیت آموزشی، حمایت از نهادهای مدنی و ارتقای سواد رسانه‌ای از اولویت‌های اصلی سیاست‌گذاری اجتماعی است.</p>
            <h3>چالش‌های اجتماعی</h3>
            <p>آسیب‌های اجتماعی، نابرابری درآمدی، مهاجرت داخلی، فقر و محرومیت در مناطق کمتر توسعه‌یافته و آلودگی محیط‌زیست از چالش‌های پیش روی جامعه امروز ایران است. مقابله با این چالش‌ها نیازمند رویکرد چندبعدی و مشارکت همه بخش‌ها است.</p>
            """;

        var enContentJameeh = """
            <h2>Iranian Society and Social Transformations</h2>
            <p>Iranian society in the current century has undergone profound social, economic, and cultural transformations. The growth of urbanization, the expansion of higher education, increased access to information technology, and changing family patterns are among the most important components of these transformations.</p>
            <h3>Demographic Structure</h3>
            <ul>
            <li><strong>Young Population:</strong> Over 60% of Iran's population is under 30 years old, representing a golden opportunity for human development.</li>
            <li><strong>Urbanization:</strong> More than 75% of the population lives in cities, requiring appropriate urban planning.</li>
            <li><strong>Literacy Rate:</strong> Over 97% literacy rate with significant growth in university graduates.</li>
            <li><strong>Aging Population:</strong> In the coming decades, we will face the challenge of an aging population.</li>
            </ul>
            <h3>Social Institutions</h3>
            <p>Family, education, universities, media, and civil institutions play a decisive role in shaping the social identity of youth. Strengthening the family institution, improving educational quality, supporting civil institutions, and enhancing media literacy are among the top priorities of social policymaking.</p>
            <h3>Social Challenges</h3>
            <p>Social harms, income inequality, internal migration, poverty and deprivation in less developed regions, and environmental pollution are among the challenges facing today's Iranian society. Addressing these challenges requires a multidimensional approach and the participation of all sectors.</p>
            """;

        var arContentJameeh = """
            <h2>المجتمع الإيراني والتحولات الاجتماعية</h2>
            <p>خاض المجتمع الإيراني في القرن الحالي تحولات اجتماعية واقتصادية وثقافية عميقة. ويعتبر نمو التحضر، وتوسيع التعليم العالي، وزيادة الوصول إلى تكنولوجيا المعلومات، وتغير الأنماط العائلية من أهم مكونات هذه التحولات.</p>
            <h3>الهيكل الديموغرافي</h3>
            <ul>
            <li><strong>السكان الشباب:</strong> أكثر من 60% من سكان إيران تقل أعمارهم عن 30 عاماً، مما يمثل فرصة ذهبية للتنمية البشرية.</li>
            <li><strong>التحضر:</strong> يعيش أكثر من 75% من السكان في المدن، مما يتطلب تخطيطاً حضرياً مناسباً.</li>
            <li><strong>معدل الإلمام بالقراءة والكتابة:</strong> يزيد عن 97% مع نمو كبير في الخريجين الجامعيين.</li>
            <li><strong>شيخوخة السكان:</strong> في العقود القادمة، سنواجه تحدي شيخوخة السكان.</li>
            </ul>
            <h3>المؤسسات الاجتماعية</h3>
            <p>تلعب الأسرة، والتعليم، والجامعات، والإعلام، والمؤسسات المدنية دوراً حاسماً في تشكيل الهوية الاجتماعية للشباب. ويعتبر تعزيز مؤسسة الأسرة، وتحسين الجودة التعليمية، ودعم المؤسسات المدنية، وتعزيز محو الأمية الإعلامية من بين الأولويات القصوى لصنع السياسات الاجتماعية.</p>
            <h3>التحديات الاجتماعية</h3>
            <p>تعتبر الأضرار الاجتماعية، وعدم المساواة في الدخل، والهجرة الداخلية، والفقر والحرمان في المناطق الأقل نمواً، والتلوث البيئي من بين التحديات التي تواجه المجتمع الإيراني اليوم. ومواجهة هذه التحديات تتطلب نهجاً متعدد الأبعاد ومشاركة جميع القطاعات.</p>
            """;

        var faContentHokmrani = """
            <h2>حکمرانی خوب و توسعه پایدار</h2>
            <p>حکمرانی مطلوب به عنوان یکی از محورهای اصلی توسعه پایدار در سندهای بالندگی و برنامه‌های توسعه ایران جایگاه ویژه‌ای دارد. ارتقای شفافیت، پاسخگویی، مشارکت مدنی و اصلاح ساختاری به عنوان مؤلفه‌های حکمرانی خوب در نظر گرفته می‌شوند.</p>
            <h3>ابعاد حکمرانی خوب</h3>
            <ul>
            <li><strong>شفافیت:</strong> آزادی اطلاعات، درآمد و هزینه‌های عمومی، اعلام تصمیمات و سیاست‌ها به صورت شفاف.</li>
            <li><strong>پاسخگویی:</strong> دستگاه‌های دولتی در برابر افکار عمومی و نهادهای نظارتی پاسخگو باشند.</li>
            <li><strong>مشارکت مدنی:</strong> مشارکت فعال شهروندان در فرآیند تصمیم‌گیری و نظارت بر اجرای سیاست‌ها.</li>
            <li><strong>حاکمیت قانون:</strong> همه افراد و نهادها تابع قانون بوده و هیچ کس فوق قانون نیست.</li>
            <li><strong>عدالت و برابری:</strong> دسترسی عادلانه به فرصت‌ها و خدمات دولتی برای همه شهروندان بدون تبعیض.</li>
            </ul>
            <h3>اصلاحات اداری</h3>
            <p>در سال‌های اخیر اصلاحات مهمی در ساختار دستگاه اجرایی صورت گرفته است: ساده‌سازی سازوکارها، توسعه خدمات الکترونیکی دولت، کاهش مراکز تصمیم‌گیری، ارتقای نیروی انسانی و سیستم‌های ارزیابی عملکرد. این اصلاحات هدفمند در جهت افزایش بهره‌وری و کیفیت خدمات دولتی در جریان هستند.</p>
            <h3>نظارت و کنترل</h3>
            <p>نهادهای نظارتی همچون قوه قضائیه، حسابرسان کل، سازمان تعزیرات حکومتی و نهادهای مدنی و رسانه‌ها نقش کلیدی در پیشگیری از فساد و ارتقای حکمرانی خوب ایفا می‌کنند. تقویت این نهادها و تجهیز آنها به ابزارهای نوین نظارتی ضروری است.</p>
            """;

        var enContentHokmrani = """
            <h2>Good Governance and Sustainable Development</h2>
            <p>Good governance holds a special position as one of the main pillars of sustainable development in Iran's vision documents and development plans. Promoting transparency, accountability, civil participation, and structural reform are considered key components of good governance.</p>
            <h3>Dimensions of Good Governance</h3>
            <ul>
            <li><strong>Transparency:</strong> Freedom of information, public revenues and expenditures, transparent announcement of decisions and policies.</li>
            <li><strong>Accountability:</strong> Government bodies are accountable to public opinion and oversight institutions.</li>
            <li><strong>Civil Participation:</strong> Active participation of citizens in the decision-making process and monitoring policy implementation.</li>
            <li><strong>Rule of Law:</strong> All individuals and institutions are subject to the law; no one is above the law.</li>
            <li><strong>Justice and Equality:</strong> Fair access to opportunities and government services for all citizens without discrimination.</li>
            </ul>
            <h3>Administrative Reforms</h3>
            <p>In recent years, important reforms have taken place in the structure of the executive branch: simplifying mechanisms, expanding electronic government services, reducing decision-making layers, improving human resources, and performance evaluation systems. These targeted reforms are underway to increase the productivity and quality of government services.</p>
            <h3>Oversight and Control</h3>
            <p>Oversight institutions such as the Judiciary, General Audit Office, State Inspectorate Organization, as well as civil institutions and media play a key role in preventing corruption and promoting good governance. Strengthening these institutions and equipping them with modern monitoring tools is essential.</p>
            """;

        var arContentHokmrani = """
            <h2>الحوكمة الرشيدة والتنمية المستدامة</h2>
            <p>تحتل الحوكمة الرشيدة مكانة خاصة كأحد الركائز الرئيسية للتنمية المستدامة في وثائق الرؤية وخطط التنمية الإيرانية. ويعتبر تعزيز الشفافية والمساءلة والمشاركة المدنية والإصلاح الهيكلي من المكونات الأساسية للحوكمة الرشيدة.</p>
            <h3>أبعاد الحوكمة الرشيدة</h3>
            <ul>
            <li><strong>الشفافية:</strong> حرية المعلومات، والإيرادات والنفقات العامة، والإعلان الشفاف عن القرارات والسياسات.</li>
            <li><strong>المساءلة:</strong> الجهات الحكومية مسؤولة أمام الرأي العام ومؤسسات الرقابة.</li>
            <li><strong>المشاركة المدنية:</strong> المشاركة النشطة للمواطنين في عملية صنع القرار ومراقبة تنفيذ السياسات.</li>
            <li><strong>سيادة القانون:</strong> جميع الأفراد والمؤسسات خاضعة للقانون؛ ولا يوجد أحد فوق القانون.</li>
            <li><strong>العدالة والمساواة:</strong> الوصول العادل إلى الفرص والخدمات الحكومية لجميع المواطنين دون تمييز.</li>
            </ul>
            <h3>الإصلاحات الإدارية</h3>
            <p>في السنوات الأخيرة، أجريت إصلاحات هامة في هيكل السلطة التنفيذية: تبسيط الآليات، وتوسيع خدمات الحكومة الإلكترونية، وتقليل طبقات صنع القرار، وتحسين الموارد البشرية، وأنظمة تقييم الأداء. وهذه الإصلاحات المستهدفة جارية لزيادة إنتاجية وجودة الخدمات الحكومية.</p>
            <h3>الرقابة والتحكم</h3>
            <p>تلعب مؤسسات الرقابة مثل السلطة القضائية، والجهاز المركزي للمحاسبات، وهيئة التفتيش الحكومية، وكذلك المؤسسات المدنية ووسائل الإعلام، دوراً أساسياً في منع الفساد وتعزيز الحوكمة الرشيدة. وإن تعزيز هذه المؤسسات وتجهيزها بأدوات الرقابة الحديثة أمر ضروري.</p>
            """;

        var faContentCheshmandaz = """
            <h2>چشم‌انداز ایران ۱۴۱۴ و اهداف کلان</h2>
            <p>سند چشم‌انداز بیست‌ساله ایران با هدف تعیین جایگاه مطلوب کشور در افق بیست‌ساله تهیه شده است. این سند ایران را کشوری پیشرفته، دارای تأثیرگذاری اول در منطقه، و فعال و مؤثر در تعاملات بین‌الملل تصور می‌کند.</p>
            <h3>اهداف کلان چشم‌انداز</h3>
            <ul>
            <li><strong>اقتصادی:</strong> تبدیل شدن به اولین اقتصاد بزرگ منطقه با رشد اقتصادی پایدار و توسعه‌یافته.</li>
            <li><strong>علمی و فناور:</strong> تبدیل شدن به قطب علم و فناوری در منطقه با تأکید بر فناوری‌های نوین.</li>
            <li><strong>فرهنگی:</strong> پیشگویی در عرصه فرهنگی و تقویت هویت اسلامی ایرانی در عرصه جهانی.</li>
            <li><strong>سیاسی:</strong> افزایش قدرت نرم و سخت کشوری در عرصه منطقه‌ای و بین‌المللی.</li>
            <li><strong>اجتماعی:</strong> رسیدن به سطح رفاه اجتماعی بالا و عدالت در توزیع درآمد و امکانات.</li>
            </ul>
            <h3>محورهای اصلی توسعه</h3>
            <p>تحقق چشم‌انداز مستلزم حرکت همزمان در محورهای مختلف است: توسعه زیرساختی جامع در فناوری اطلاعات، حمل‌ونقل و انرژی؛ توسعه منابع انسانی با تأکید بر آموزش مهارت‌های قرن بیست‌یکم؛ توسعه مناطق کمتر توسعه‌یافته و کاهش نابرابری‌های منطقه‌ای؛ و ارتقای بهره‌وری کل عوامل تولید در کل اقتصاد.</p>
            <h3>نقش نسل جوان</h3>
            <p>بدون تردید مهم‌ترین سرمایه برای تحقق چشم‌انداز، نیروی انسانی جوان و تحصیلکرده کشور است. ایجاد بستر مناسب برای خلق ثروت، کارآفرینی، نوآوری و جذب جوانان مستعد در واحدهای تولیدی و علمی از ضروریات اصلی راهبردی این دوران به شمار می‌رود.</p>
            """;

        var enContentCheshmandaz = """
            <h2>Iran's 1414 Vision and Major Goals</h2>
            <p>Iran's Twenty-Year Vision Document has been prepared to determine the country's desired position over a twenty-year horizon. This document envisions Iran as an advanced country, having primary influence in the region, and being active and effective in international interactions.</p>
            <h3>Major Vision Goals</h3>
            <ul>
            <li><strong>Economic:</strong> Becoming the first largest economy in the region with sustainable and developed economic growth.</li>
            <li><strong>Science and Technology:</strong> Becoming a pole of science and technology in the region with emphasis on emerging technologies.</li>
            <li><strong>Cultural:</strong> Pioneering in the cultural arena and strengthening Iranian-Islamic identity on the global stage.</li>
            <li><strong>Political:</strong> Increasing the country's soft and hard power in regional and international arenas.</li>
            <li><strong>Social:</strong> Achieving a high level of social welfare and justice in income distribution and facilities.</li>
            </ul>
            <h3>Main Development Axes</h3>
            <p>Realizing the vision requires simultaneous movement across various axes: comprehensive infrastructure development in information technology, transportation and energy; human resource development with emphasis on 21st-century skill training; development of less developed regions and reduction of regional inequalities; and improving total factor productivity across the economy.</p>
            <h3>Role of the Young Generation</h3>
            <p>Undoubtedly, the most important capital for achieving the vision is the country's young and educated workforce. Creating the appropriate platform for wealth creation, entrepreneurship, innovation, and attracting talented youth to productive and scientific units is considered one of the main strategic imperatives of this era.</p>
            """;

        var arContentCheshmandaz = """
            <h2>رؤية إيران 1414 والأهداف الكبرى</h2>
            <p>أعدت وثيقة الرؤية الإيرانية لمدة عشرين عاماً لتحديد المكانة المرغوبة للبلاد على مدى أفق عشرين عاماً. وتتصور هذه الوثيقة إيران كبلاد متقدمة، ذات تأثير أساسي في المنطقة، ونشطة وفعالة في التفاعلات الدولية.</p>
            <h3>الأهداف الكبرى للرؤية</h3>
            <ul>
            <li><strong>اقتصادي:</strong> أن تصبح أول أكبر اقتصاد في المنطقة بنمو اقتصادي مستدام ومتطور.</li>
            <li><strong>علمي وتكنولوجي:</strong> أن تصبح قطباً للعلم والتكنولوجيا في المنطقة مع التأكيد على التقنيات الناشئة.</li>
            <li><strong>ثقافي:</strong> الريادة في المجال الثقافي وتعزيز الهوية الإسلامية الإيرانية على الساحة العالمية.</li>
            <li><strong>سياسي:</strong> زيادة القوة الناعمة والصلبة للبلاد في الساحتين الإقليمية والدولية.</li>
            <li><strong>اجتماعي:</strong> الوصول إلى مستوى عالٍ من الرفاه الاجتماعي والعدالة في توزيع الدخل والمرافق.</li>
            </ul>
            <h3>المحاور الرئيسية للتنمية</h3>
            <p>يتطلب تحقيق الرؤية الحركة المتزامنة عبر محاور متنوعة: تطوير البنية التحتية الشاملة في تكنولوجيا المعلومات والنقل والطاقة؛ وتطوير الموارد البشرية مع التأكيد على تدريب مهارات القرن الحادي والعشرين؛ وتطوير المناطق الأقل نمواً وتقليل عدم المساواة الإقليمية؛ وتحسين إنتاجية العوامل الإجمالية في الاقتصاد ككل.</p>
            <h3>دور الجيل الشاب</h3>
            <p>مما لا شك فيه أن أهم رأس مال لتحقيق الرؤية هو القوى العاملة الشابة والمتعلمة في البلاد. ويعتبر خلق المنصة المناسبة لخلق الثروة، وريادة الأعمال، والابتكار، وجذب الشباب الموهوبين إلى الوحدات الإنتاجية والعلمية من بين الضروريات الاستراتيجية الرئيسية لهذا العصر.</p>
            """;

        var faContentToseehMelli = """
            <h2>توسعه ملی و برنامه‌های شصت‌ساله</h2>
            <p>برنامه‌های توسعه ملی ایران با هدف برنامه‌ریزی برای توسعه همه‌جانبه اقتصادی، اجتماعی و فرهنگی کشور به صورت دوره‌ای تدوین و اجرا می‌شوند. این برنامه‌ها بر اساس اهداف سند چشم‌انداز و با مشارکت کارشناسان و ذی‌نفعان مختلف تهیه می‌گردند.</p>
            <h3>محورهای اصلی برنامه توسعه هفتم</h3>
            <ul>
            <li><strong>دانش‌بنیان کردن اقتصاد:</strong> تأکید بر فناوری‌های پیشرفته، استارتاپ‌ها و شرکت‌های دانش‌بنیان.</li>
            <li><strong>توسعه زیرساخت:</strong> تکمیل پروژه‌های حمل‌ونقل، انرژی، آب و فناوری اطلاعات.</li>
            <li><strong>ارتقای بهره‌وری:</strong> بهبود بهره‌وری نیروی کار، سرمایه و انرژی در همه بخش‌ها.</li>
            <li><strong>توسعه کشاورزی و امنیت غذایی:</strong> افزایش تولید و بهبود زنجیره تأمین مواد غذایی.</li>
            <li><strong>حفاظت محیط‌زیست:</strong> کنترل آلودگی‌ها و مدیریت منابع آب و خاک و جنگل‌ها و مراتع.</li>
            </ul>
            <h3>چالش‌های توسعه</h3>
            <p>تحریم‌ها، محدودیت منابع آبی، تغییر اقلیم، نوسانات قیمت نفت و سوگیری‌های اقتصادی از مهم‌ترین موانع توسعه در سال‌های اخیر بوده‌اند. اما ظرفیت‌های نهفته در بخش‌های کشاورزی، معدنی، صنایع بالادستی و دانش‌بنیان می‌تواند جایگزین مناسبی برای درآمدهای نفتی باشد.</p>
            <h3>نقش بخش خصوصی</h3>
            <p>تقویت نقش بخش خصوصی به عنوان موتور توسعه و کاهش سهم دولت از اقتصاد، از اصول اساسی برنامه‌های توسعه اخیر است. خصوصی‌سازی، تسهیل ورود به کسب‌وکار و حمایت از سرمایه‌گذاران داخلی و خارجی از موارد تأکید شده در این زمینه هستند.</p>
            """;

        var enContentToseehMelli = """
            <h2>National Development and Multi-Year Plans</h2>
            <p>Iran's national development plans are periodically drafted and implemented with the aim of planning for comprehensive economic, social, and cultural development of the country. These plans are prepared based on the objectives of the Vision Document with the participation of experts and various stakeholders.</p>
            <h3>Main Axes of the 7th Development Plan</h3>
            <ul>
            <li><strong>Knowledge-Based Economy:</strong> Emphasis on advanced technologies, startups, and knowledge-based companies.</li>
            <li><strong>Infrastructure Development:</strong> Completing transportation, energy, water, and IT projects.</li>
            <li><strong>Productivity Improvement:</strong> Improving labor, capital, and energy productivity in all sectors.</li>
            <li><strong>Agricultural Development and Food Security:</strong> Increasing production and improving the food supply chain.</li>
            <li><strong>Environmental Protection:</strong> Controlling pollution and managing water, soil, forest, and rangeland resources.</li>
            </ul>
            <h3>Development Challenges</h3>
            <p>Sanctions, water resource limitations, climate change, oil price fluctuations, and economic distortions have been among the most important development obstacles in recent years. However, the latent capacities in agriculture, mining, upstream industries, and knowledge-based sectors can serve as a suitable replacement for oil revenues.</p>
            <h3>Role of the Private Sector</h3>
            <p>Strengthening the private sector as the engine of development and reducing the government's share of the economy is one of the fundamental principles of recent development plans. Privatization, facilitating business entry, and supporting domestic and foreign investors are emphasized in this regard.</p>
            """;

        var arContentToseehMelli = """
            <h2>التنمية الوطنية والخطط متعددة السنوات</h2>
            <p>تُصاغ وتُنفذ خطط التنمية الوطنية الإيرانية بشكل دوري بهدف التخطيط للتنمية الشاملة الاقتصادية والاجتماعية والثقافية للبلاد. وتُعد هذه الخطط بناءً على أهداف وثيقة الرؤية بمشاركة الخبراء وأصحاب المصلحة المختلفين.</p>
            <h3>المحاور الرئيسية للخطة السابعة للتنمية</h3>
            <ul>
            <li><strong>الاقتصاد القائم على المعرفة:</strong> التأكيد على التقنيات المتقدمة والشركات الناشئة والشركات القائمة على المعرفة.</li>
            <li><strong>تطوير البنية التحتية:</strong> إكمال مشاريع النقل والطاقة والمياه وتكنولوجيا المعلومات.</li>
            <li><strong>تحسين الإنتاجية:</strong> تحسين إنتاجية العمالة ورأس المال والطاقة في جميع القطاعات.</li>
            <li><strong>التنمية الزراعية والأمن الغذائي:</strong> زيادة الإنتاج وتحسين سلسلة التوريد الغذائي.</li>
            <li><strong>حماية البيئة:</strong> مكافحة التلوث وإدارة موارد المياه والتربة والغابات والمراعي.</li>
            </ul>
            <h3>تحديات التنمية</h3>
            <p>لقد كانت العقوبات، ومحدودية موارد المياه، وتغير المناخ، وتقلبات أسعار النفط، والانحرافات الاقتصادية من بين أهم العقبات أمام التنمية في السنوات الأخيرة. ولكن القدرات الكامنة في قطاعات الزراعة والتعدين والصناعات العلوية والقطاعات القائمة على المعرفة يمكن أن تكون بديلاً مناسباً للإيرادات النفطية.</p>
            <h3>دور القطاع الخاص</h3>
            <p>يُعد تعزيز القطاع الخاص كمحرك للتنمية وتقليل حصة الحكومة من الاقتصاد أحد المبادئ الأساسية لخطط التنمية الأخيرة. ويتم التأكيد على الخصخصة، وتسهيل دخول الأعمال، ودعم المستثمرين المحليين والأجانب في هذا الصدد.</p>
            """;

        var faContentRevabet = """
            <h2>روابط بین‌الملل ایران و دیپلماسی فعال</h2>
            <p>سیاست خارجی ایران در سال‌های اخیر با تأکید بر دیپلماسی فعال، سیاست‌گذاری هوشمندانه و توسعه همکاری‌های چندجانبه شکل گرفته است. توافق هسته‌ای، بازگشت به سازمان‌های بین‌المللی، توسعه روابط با همسایگان و راهبرد "نگاه به شرق" از مصادیق این رویکرد جدید هستند.</p>
            <h3>اولویت‌های دیپلماسی</h3>
            <ul>
            <li><strong>همسایگی:</strong> توسعه روابط با کشورهای همسایه در زمینه انرژی، حمل‌ونقل، تجارت و امور امنیتی.</li>
            <li><strong>شرکای استراتژیک:</strong> تقویت روابط با روسیه، چین، هند و برزیل به عنوان شرکای کلیدی.</li>
            <li><strong>اروپا:</strong> حفظ و توسعه روابط با کشورهای اروپایی در شرایط پس از توافق هسته‌ای.</li>
            <li><strong>آفریقا و آمریکای لاتین:</strong> توسعه روابط با کشورهای جنوب جهانی و اقتصادهای نوظهور.</li>
            <li><strong>سازمان‌های منطقه‌ای:</strong> فعال در سازمان‌هایی چون اوِپک، اوِپک پلاس، سازمان همکاری اسلامی، عدم تعهد و سکو.</li>
            </ul>
            <h3>توافق هسته‌ای برجام</h3>
            <p>برجام یا توافق جامع اقدام مشترک در سال ۲۰۱۵ بین ایران و گروه ۱+۵ به امضا رسید و باعث شد بخشی از تحریم‌های بین‌المللی علیه ایران برداشته شود و ایران از نظر فنی تعهدات هسته‌ای خود را محدود نماید. این توافق از مهم‌ترین دستاوردهای دیپلماسی ایران در سال‌های اخیر به شمار می‌رود.</p>
            <h3>چالش‌های دیپلماسی</h3>
            <p>تحریم‌های یکجانبه آمریکا، بحران‌های منطقه‌ای، اختلافات فکری با برخی قدرت‌ها و مسأله جنگ اطلاعاتی از مهم‌ترین چالش‌های پیش روی دیپلماسی ایران هستند. اما ظرفیت‌های انرژی، موقعیت ژئوپلیتیک، ارتباطات فرهنگی و تاریخی و نیروی انسانی توانمند کشور امکان غلبه بر این چالش‌ها را فراهم می‌سازد.</p>
            """;

        var enContentRevabet = """
            <h2>Iran's International Relations and Active Diplomacy</h2>
            <p>Iran's foreign policy in recent years has been shaped with emphasis on active diplomacy, smart policymaking, and expanding multilateral cooperation. The nuclear agreement, return to international organizations, expanding relations with neighbors, and the "Look to the East" strategy are examples of this new approach.</p>
            <h3>Diplomatic Priorities</h3>
            <ul>
            <li><strong>Neighborhood Policy:</strong> Developing relations with neighboring countries in energy, transportation, trade, and security affairs.</li>
            <li><strong>Strategic Partners:</strong> Strengthening relations with Russia, China, India, and Brazil as key partners.</li>
            <li><strong>Europe:</strong> Maintaining and developing relations with European countries in the post-nuclear-deal era.</li>
            <li><strong>Africa and Latin America:</strong> Expanding relations with Global South countries and emerging economies.</li>
            <li><strong>Regional Organizations:</strong> Actively participating in organizations such as OPEC, OPEC+, OIC, NAM, and the Shanghai Cooperation Organization.</li>
            </ul>
            <h3>The JCPOA Nuclear Deal</h3>
            <p>The JCPOA (Joint Comprehensive Plan of Action) was signed in 2015 between Iran and the P5+1 group, resulting in the lifting of part of international sanctions against Iran while Iran technically limited its nuclear commitments. This agreement is considered one of the most important achievements of Iranian diplomacy in recent years.</p>
            <h3>Diplomatic Challenges</h3>
            <p>Unilateral US sanctions, regional crises, ideological differences with certain powers, and information warfare are among the most important challenges facing Iranian diplomacy. However, the country's energy capacity, geopolitical position, cultural and historical ties, and capable human resources make it possible to overcome these challenges.</p>
            """;

        var arContentRevabet = """
            <h2>العلاقات الدولية لإيران والدبلوماسية النشطة</h2>
            <p>تشكلت السياسة الخارجية لإيران في السنوات الأخيرة مع التأكيد على الدبلوماسية النشطة، وصنع السياسات الذكية، وتوسيع التعاون متعدد الأطراف. ويعتبر الاتفاق النووي، والعودة إلى المنظمات الدولية، وتوسيع العلاقات مع الجيران، واستراتيجية "النشر إلى الشرق" أمثلة على هذا النهج الجديد.</p>
            <h3>أولويات الدبلوماسية</h3>
            <ul>
            <li><strong>سياسة الجوار:</strong> تطوير العلاقات مع الدول المجاورة في مجالات الطاقة والنقل والتجارة والشؤون الأمنية.</li>
            <li><strong>الشركاء الاستراتيجيون:</strong> تعزيز العلاقات مع روسيا والصين والهند والبرازيل كشركاء أساسيين.</li>
            <li><strong>أوروبا:</strong> الحفاظ على العلاقات وتطويرها مع الدول الأوروبية في عصر ما بعد الاتفاق النووي.</li>
            <li><strong>أفريقيا وأمريكا اللاتينية:</strong> توسيع العلاقات مع دول الجنوب العالمي والاقتصادات الناشئة.</li>
            <li><strong>المنظمات الإقليمية:</strong> المشاركة النشطة في منظمات مثل أوبك وأوبك بلس ومنظمة التعاون الإسلامي وحركة عدم الانحياز ومنظمة شنغهاي للتعاون.</li>
            </ul>
            <h3>اتفاق خطة العمل الشاملة المشتركة (خطة العمل المشتركة)</h3>
            <p>تم توقيع خطة العمل الشاملة المشتركة في عام 2015 بين إيران ومجموعة الخمسة الدائمين زائد ألمانيا، مما أدى إلى رفع جزء من العقوبات الدولية المفروضة على إيران بينما حدت إيران فنانياً من التزاماتها النووية. ويُعتبر هذا الاتفاق أحد أهم الإنجازات الدبلوماسية الإيرانية في السنوات الأخيرة.</p>
            <h3>تحديات الدبلوماسية</h3>
            <p>تعتبر العقوبات الأمريكية الأحادية الجانب، والأزمات الإقليمية، والخلافات الأيديولوجية مع بعض القوى، والحرب الإعلامية من بين أهم التحديات التي تواجه الدبلوماسية الإيرانية. ولكن القدرة الطاقوية للبلاد، والموقع الجيوسياسي، والروابط الثقافية والتاريخية، والموارد البشرية المؤهلة، كلها عوامل تمكّن من التغلب على هذه التحديات.</p>
            """;

        var seedPosts = new (
            string FaTitle, string FaSlug, string FaDesc, string FaContent,
            string EnTitle, string EnSlug, string EnDesc, string EnContent,
            string ArTitle, string ArSlug, string ArDesc, string ArContent)[]
        {
            (
                "سیاست",
                "siasat",
                "بررسی سیاست داخلی و خارجی ایران در عصر جدید",
                faContentSiasat,
                "Politics",
                "politics",
                "Analysis of Iran's domestic and foreign politics in the new era",
                enContentSiasat,
                "السياسة",
                "al-siyasah",
                "تحليل السياسة الداخلية والخارجية لإيران في العصر الجديد",
                arContentSiasat
            ),
            (
                "جامعه",
                "jame'eh",
                "تحولات اجتماعی، ساختار جمعیتی و نهادهای جامعه ایران",
                faContentJameeh,
                "Society",
                "society",
                "Social transformations, demographic structure and social institutions of Iran",
                enContentJameeh,
                "المجتمع",
                "al-mujtama",
                "التحولات الاجتماعية، الهيكل الديموغرافي والمؤسسات الاجتماعية في إيران",
                arContentJameeh
            ),
            (
                "حکمرانی",
                "hokmrani",
                "حکمرانی خوب، اصلاحات اداری و چشم‌انداز توسعه پایدار",
                faContentHokmrani,
                "Governance",
                "governance",
                "Good governance, administrative reforms and sustainable development vision",
                enContentHokmrani,
                "الحوكمة",
                "al-hukamah",
                "الحوكمة الرشيدة، الإصلاحات الإدارية ورؤية التنمية المستدامة",
                arContentHokmrani
            ),
            (
                "چشم‌انداز ایران",
                "cheshmandaz-e-iran",
                "سند چشم‌انداز بیست‌ساله ایران و اهداف کلان توسعه",
                faContentCheshmandaz,
                "Iran's Vision",
                "irans-vision",
                "Iran's 20-year vision document and major development goals",
                enContentCheshmandaz,
                "رؤية إيران",
                "ruyat-iran",
                "وثيقة الرؤية الإيرانية لمدة عشرين عاماً والأهداف الكبرى للتنمية",
                arContentCheshmandaz
            ),
            (
                "توسعه ملی",
                "tose'eh-ye-melli",
                "برنامه‌های توسعه ملی ایران و چالش‌ها و راه‌کارهای آن",
                faContentToseehMelli,
                "National Development",
                "national-development",
                "Iran's national development plans, challenges and solutions",
                enContentToseehMelli,
                "التنمية الوطنية",
                "al-tanmiyah-al-wataniyah",
                "خطط التنمية الوطنية الإيرانية وتحدياتها وحلولها",
                arContentToseehMelli
            ),
            (
                "روابط بین‌الملل",
                "revabet-e-beynolmelal",
                "روابط بین‌الملل ایران، دیپلماسی فعال و اولویت‌های خارجی",
                faContentRevabet,
                "International Relations",
                "international-relations",
                "Iran's international relations, active diplomacy and foreign priorities",
                enContentRevabet,
                "العلاقات الدولية",
                "al-alaqat-al-dawliyah",
                "العلاقات الدولية لإيران، والدبلوماسية النشطة والأولويات الخارجية",
                arContentRevabet
            ),
        };

        var existingTranslations = await db.BlogPostTranslations
            .Include(t => t.Post)
            .Where(t => t.LanguagePrefix == "fa")
            .ToDictionaryAsync(t => t.Slug, t => t, cancellationToken);

        foreach (var post in seedPosts)
        {
            Guid postId;
            if (existingTranslations.TryGetValue(post.FaSlug, out var existingFaTr))
            {
                postId = existingFaTr.PostId;

                var faTr = await db.BlogPostTranslations
                    .FirstOrDefaultAsync(t => t.PostId == postId && t.LanguagePrefix == "fa", cancellationToken);
                var enTr = await db.BlogPostTranslations
                    .FirstOrDefaultAsync(t => t.PostId == postId && t.LanguagePrefix == "en", cancellationToken);
                var arTr = await db.BlogPostTranslations
                    .FirstOrDefaultAsync(t => t.PostId == postId && t.LanguagePrefix == "ar", cancellationToken);

                if (faTr is not null && (faTr.Content is null || faTr.Content.Length < 50))
                {
                    faTr.Update(post.FaTitle, post.FaSlug, post.FaTitle, post.FaDesc, post.FaContent, post.FaTitle, post.FaDesc);
                }
                if (enTr is not null && (enTr.Content is null || enTr.Content.Length < 50))
                {
                    enTr.Update(post.EnTitle, post.EnSlug, post.EnTitle, post.EnDesc, post.EnContent, post.EnTitle, post.EnDesc);
                }
                if (arTr is not null && (arTr.Content is null || arTr.Content.Length < 50))
                {
                    arTr.Update(post.ArTitle, post.ArSlug, post.ArTitle, post.ArDesc, post.ArContent, post.ArTitle, post.ArDesc);
                }

                var existingPost = existingFaTr.Post;
                if (existingPost is not null && (existingPost.Content is null || existingPost.Content.Length < 50))
                {
                    existingPost.Update(
                        post.FaTitle, post.FaSlug, post.FaTitle, post.FaDesc, post.FaContent,
                        post.FaTitle, post.FaDesc, BlogPostStatus.Published, true);
                }

                await db.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Updated content for existing post: {FaTitle} ({EnTitle})", post.FaTitle, post.EnTitle);
                continue;
            }

            // Skip insert when EN/AR slug already exists (partial prior seed / slug rename).
            var slugTaken = await db.BlogPostTranslations.AnyAsync(
                t => (t.LanguagePrefix == "en" && t.Slug == post.EnSlug)
                     || (t.LanguagePrefix == "ar" && t.Slug == post.ArSlug),
                cancellationToken);
            if (slugTaken)
            {
                logger.LogWarning(
                    "Skipping seed post {FaSlug}: EN/AR slug already exists ({EnSlug}/{ArSlug}).",
                    post.FaSlug, post.EnSlug, post.ArSlug);
                continue;
            }

            var blogPost = BlogPost.Create(
                title: post.FaTitle,
                slug: post.FaSlug,
                keyword: post.FaTitle,
                description: post.FaDesc,
                content: post.FaContent,
                metaTitle: post.FaTitle,
                metaDescription: post.FaDesc,
                status: BlogPostStatus.Published,
                commentsEnabled: true);
            db.BlogPosts.Add(blogPost);
            await db.SaveChangesAsync(cancellationToken);
            postId = blogPost.Id;

            db.BlogPostTranslations.Add(BlogPostTranslation.Create(
                postId, "fa",
                post.FaTitle, post.FaSlug,
                post.FaTitle, post.FaDesc, post.FaContent, post.FaTitle, post.FaDesc));
            db.BlogPostTranslations.Add(BlogPostTranslation.Create(
                postId, "en",
                post.EnTitle, post.EnSlug,
                post.EnTitle, post.EnDesc, post.EnContent, post.EnTitle, post.EnDesc));
            db.BlogPostTranslations.Add(BlogPostTranslation.Create(
                postId, "ar",
                post.ArTitle, post.ArSlug,
                post.ArTitle, post.ArDesc, post.ArContent, post.ArTitle, post.ArDesc));

            db.BlogPostGroups.Add(BlogPostGroup.Create(postId, groupId));
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded blog post with content: {FaTitle} ({EnTitle})", post.FaTitle, post.EnTitle);
        }
    }

    private static async Task SeedPoliticsAndVisionMenuItemsAsync(
        ContentModulesDbContext db,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var menuGroup = await db.MenuGroups
            .FirstOrDefaultAsync(x => x.Key == "topmenu", cancellationToken);
        if (menuGroup is null)
        {
            logger.LogWarning("topmenu MenuGroup not found; skipping politics menu seed.");
            return;
        }

        var blogGroupTr = await db.BlogGroupTranslations
            .FirstOrDefaultAsync(t => t.LanguagePrefix == "fa" && t.Slug == "siasat-o-cheshmandaz", cancellationToken);
        if (blogGroupTr is null)
        {
            logger.LogWarning("BlogGroup siasat-o-cheshmandaz not found; skipping politics menu seed.");
            return;
        }

        Guid blogGroupId = blogGroupTr.GroupId;

        var postSlugsWithInfo = new (string FaSlug, string FaTitle, string EnTitle, string ArTitle, string EnSlug, string ArSlug)[]
        {
            ("siasat", "سیاست", "Politics", "السياسة", "politics", "al-siyasah"),
            ("jame'eh", "جامعه", "Society", "المجتمع", "society", "al-mujtama"),
            ("hokmrani", "حکمرانی", "Governance", "الحوكمة", "governance", "al-hukamah"),
            ("cheshmandaz-e-iran", "چشم‌انداز ایران", "Iran's Vision", "رؤية إيران", "irans-vision", "ruyat-iran"),
            ("tose'eh-ye-melli", "توسعه ملی", "National Development", "التنمية الوطنية", "national-development", "al-tanmiyah-al-wataniyah"),
            ("revabet-e-beynolmelal", "روابط بین‌الملل", "International Relations", "العلاقات الدولية", "international-relations", "al-alaqat-al-dawliyah"),
        };

        var postTranslations = await db.BlogPostTranslations
            .Where(t => t.LanguagePrefix == "fa" && postSlugsWithInfo.Select(p => p.FaSlug).Contains(t.Slug))
            .ToListAsync(cancellationToken);
        var postIdsByFaSlug = postTranslations.ToDictionary(t => t.Slug, t => t.PostId);

        var parentMenuItem = await db.MenuItems
            .FirstOrDefaultAsync(x =>
                x.GroupId == menuGroup.Id &&
                (x.Function == MenuLinkFunction.GroupBlog && x.TargetId == blogGroupId ||
                 x.Url == "/politics"),
                cancellationToken);

        Guid parentId;
        if (parentMenuItem is null)
        {
            var pItem = MenuItem.Create(
                title: "سیاست و چشم‌انداز",
                url: "/politics",
                groupId: menuGroup.Id,
                parentId: null,
                data: null,
                function: MenuLinkFunction.GroupBlog,
                targetId: blogGroupId,
                isMegaMenu: false,
                sortOrder: 11,
                isActive: true);
            db.MenuItems.Add(pItem);
            await db.SaveChangesAsync(cancellationToken);
            parentId = pItem.Id;

            db.MenuItemTranslations.Add(MenuItemTranslation.Create(parentId, "fa", "سیاست و چشم‌انداز", "/politics", null));
            db.MenuItemTranslations.Add(MenuItemTranslation.Create(parentId, "en", "POLITICS & VISION", "/politics", null));
            db.MenuItemTranslations.Add(MenuItemTranslation.Create(parentId, "ar", "السياسة والرؤية", "/politics", null));
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded parent menu item for Politics & Vision.");
        }
        else
        {
            parentId = parentMenuItem.Id;
            if (parentMenuItem.Function != MenuLinkFunction.GroupBlog || parentMenuItem.TargetId != blogGroupId || parentMenuItem.SortOrder != 11)
            {
                parentMenuItem.Update(
                    parentMenuItem.Title,
                    parentMenuItem.Url,
                    parentMenuItem.ParentId,
                    parentMenuItem.Data,
                    MenuLinkFunction.GroupBlog,
                    blogGroupId,
                    parentMenuItem.IsMegaMenu,
                    11,
                    parentMenuItem.IsActive);
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        int sortIndex = 0;
        foreach (var info in postSlugsWithInfo)
        {
            if (!postIdsByFaSlug.TryGetValue(info.FaSlug, out var postId))
            {
                logger.LogWarning("Post with fa slug {Slug} not found, skipping menu seed.", info.FaSlug);
                sortIndex++;
                continue;
            }

            var faUrl = $"/blog/{info.FaSlug}";
            var enUrl = $"/blog/{info.EnSlug}";
            var arUrl = $"/blog/{info.ArSlug}";

            var existingChild = await db.MenuItems.FirstOrDefaultAsync(x =>
                x.GroupId == menuGroup.Id &&
                x.ParentId == parentId &&
                x.Function == MenuLinkFunction.Post &&
                x.TargetId == postId,
                cancellationToken);

            if (existingChild is not null)
            {
                if (existingChild.SortOrder != sortIndex)
                {
                    existingChild.Update(
                        existingChild.Title,
                        existingChild.Url,
                        existingChild.ParentId,
                        existingChild.Data,
                        existingChild.Function,
                        existingChild.TargetId,
                        existingChild.IsMegaMenu,
                        sortIndex,
                        existingChild.IsActive);
                    await db.SaveChangesAsync(cancellationToken);
                }
                sortIndex++;
                continue;
            }

            var child = MenuItem.Create(
                title: info.FaTitle,
                url: faUrl,
                groupId: menuGroup.Id,
                parentId: parentId,
                data: null,
                function: MenuLinkFunction.Post,
                targetId: postId,
                isMegaMenu: false,
                sortOrder: sortIndex,
                isActive: true);
            db.MenuItems.Add(child);
            await db.SaveChangesAsync(cancellationToken);

            db.MenuItemTranslations.Add(MenuItemTranslation.Create(child.Id, "fa", info.FaTitle, faUrl, null));
            db.MenuItemTranslations.Add(MenuItemTranslation.Create(child.Id, "en", info.EnTitle, enUrl, null));
            db.MenuItemTranslations.Add(MenuItemTranslation.Create(child.Id, "ar", info.ArTitle, arUrl, null));
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded menu child: {FaTitle} under Politics & Vision.", info.FaTitle);
            sortIndex++;
        }
    }

    private static async Task SeedImperiaDevelopmentBlogGroupAsync(
        ContentModulesDbContext db,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var groupSlugFa = "imperia-tose'eh";
        var group = await db.BlogGroups
            .Include(g => g.Translations)
            .FirstOrDefaultAsync(g => g.Slug == groupSlugFa, cancellationToken);

        Guid groupId;
        if (group is null)
        {
            group = BlogGroup.Create(
                title: "امپریا توسعه",
                slug: groupSlugFa,
                keyword: "امپریا توسعه, ساخت‌وساز, توسعه شهری, زیرساخت, پروژه‌های بزرگ",
                description: "پروژه‌های عمرانی، زیرساخت و توسعه شهری امپریا",
                parentId: null);
            db.BlogGroups.Add(group);
            await db.SaveChangesAsync(cancellationToken);
            groupId = group.Id;

            db.BlogGroupTranslations.Add(BlogGroupTranslation.Create(
                groupId, "fa", "امپریا توسعه", groupSlugFa,
                "امپریا توسعه, ساخت‌وساز, توسعه شهری, زیرساخت, پروژه‌های بزرگ", "پروژه‌های عمرانی، زیرساخت و توسعه شهری امپریا"));
            db.BlogGroupTranslations.Add(BlogGroupTranslation.Create(
                groupId, "en", "IMPERIA DEVELOPMENT", "imperia-development",
                "imperia development, construction, urban development, infrastructure, mega projects", "Imperia's construction, infrastructure and urban development projects"));
            db.BlogGroupTranslations.Add(BlogGroupTranslation.Create(
                groupId, "ar", "إيمبيريا للتنمية", "imbiria-liltanmiyah",
                "إيمبيريا للتنمية, البناء, التنمية الحضرية, البنية التحتية, المشاريع الكبرى", "مشاريع إيمبيريا للبناء والبنية التحتية والتنمية الحضرية"));
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded blog group: Imperia Development (امپریا توسعه).");
        }
        else
        {
            groupId = group.Id;
        }

        var faContent1 = """
            <h2>واحد ساخت‌وساز امپریا</h2>
            <p>بخش ساخت‌وساز امپریا با بهره‌گیری از مهندسان برجسته، تجهیزات مدرن و استانداردهای بین‌المللی، در حال اجرای پروژه‌های مقیاس‌پذیر در سراسر ایران است. این واحد با تمرکز بر کیفیت، رعایت ضوابط ایمنی و رعایت محیط‌زیست، پروژه‌های بزرگی را در کارنامه خود دارد.</p>
            <h3>انواع پروژه‌های ساخت‌وساز</h3>
            <ul>
            <li><strong>ساختمان‌های مسکونی:</strong> مجتمع‌های مسکونی لوکس و نیمه‌لوکس با استانداردهای روز دنیا.</li>
            <li><strong>ساختمان‌های اداری و تجاری:</strong> برج‌های اداری و تجاری با طراحی‌های مدرن و انرژی‌کارا.</li>
            <li><strong>مراکز خرید:</strong> طراحی و احداث مراکز خرید و مراکز تجاری چندمنظوره.</li>
            <li><strong>پروژه‌های آموزشی و درمانی:</strong> ساخت دانشکده‌ها، بیمارستان‌ها و مراکز تحقیقاتی.</li>
            <li><strong>سازه‌های صنعتی:</strong> تأسیس کارخانه‌ها، انبارها و سازه‌های صنعتی ویژه.</li>
            </ul>
            <h3>استانداردهای کیفی</h3>
            <p>بخش ساخت‌وساز امپریا در تمام پروژه‌ها از استانداردهای بین‌المللی مانند ISO 9001، ISO 14001 و استانداردهای ملی ایران استفاده می‌کند. کنترل کیفیت در تمام مراحل طراحی، تأمین مصالح، اجرا و تحویل به صورت مستمر انجام می‌شود و هر پروژه پیش از تحویل، بازرسی‌های متعددی را پشت سر می‌گذارد.</p>
            <h3>نوآوری در ساخت‌وساز</h3>
            <p>استفاده از BIM (مدل‌سازی اطلاعات ساختمان)، سازه‌های پیش‌ساخته و نیمه‌ساخته، مصالح سبک و دوستدار محیط‌زیست، سیستم‌های هوشمند ساختمان و انرژی‌های تجدیدپذیر از مهم‌ترین راهبردهای امپریا در ارتقای فناوری ساخت‌وساز است.</p>
            """;

        var enContent1 = """
            <h2>Imperia Construction Division</h2>
            <p>Imperia's construction division, leveraging distinguished engineers, modern equipment and international standards, is implementing scalable projects across Iran. Focusing on quality, safety regulations and environmental compliance, this unit has delivered major projects throughout its portfolio.</p>
            <h3>Types of Construction Projects</h3>
            <ul>
            <li><strong>Residential Buildings:</strong> Luxury and semi-luxury residential complexes built to world-class standards.</li>
            <li><strong>Office and Commercial Buildings:</strong> Modern, energy-efficient office and commercial towers.</li>
            <li><strong>Shopping Centers:</strong> Design and construction of shopping malls and mixed-use commercial centers.</li>
            <li><strong>Educational and Medical Projects:</strong> Building universities, hospitals, and research centers.</li>
            <li><strong>Industrial Structures:</strong> Establishing factories, warehouses, and special industrial facilities.</li>
            </ul>
            <h3>Quality Standards</h3>
            <p>Imperia's construction division applies international standards such as ISO 9001, ISO 14001 and Iranian national standards across all projects. Quality control is continuously performed throughout design, material procurement, execution and delivery, and each project undergoes multiple inspections before handover.</p>
            <h3>Innovation in Construction</h3>
            <p>Using BIM (Building Information Modeling), prefabricated and semi-prefabricated structures, lightweight and eco-friendly materials, smart building systems and renewable energies are among Imperia's key strategies to advance construction technology.</p>
            """;

        var arContent1 = """
            <h2>قسمت البناء والإنشاء في إيمبيريا</h2>
            <p>يقوم قسم البناء والإنشاء في إيمبيريا، بفضل الاستعانة بالمهندسين المتميزين والمعدات الحديثة والمعايير الدولية، بتنفيذ مشاريع قابلة للتوسع في جميع أنحاء إيران. مع التركيز على الجودة، واللوائح الأمنية، والامتثال البيئي، سجلت هذه الوحدة مشاريع كبرى ضمن محفظتها.</p>
            <h3>أنواع مشاريع البناء</h3>
            <ul>
            <li><strong>المباني السكنية:</strong> المجمعات السكنية الفاخرة وشبه الفاخرة المبنية وفقاً لأعلى المعايير العالمية.</li>
            <li><strong>المباني المكتبية والتجارية:</strong> أبراج مكاتب وتجارية حديثة موفرة للطاقة.</li>
            <li><strong>مراكز التسوق:</strong> تصميم وبناء مراكز تسوق ومجمعات تجارية متعددة الاستخدامات.</li>
            <li><strong>مشاريع تعليمية وطبية:</strong> بناء الجامعات والمستشفيات ومراكز الأبحاث.</li>
            <li><strong>المنشآت الصناعية:</strong> إنشاء المصانع والمستودعات والمرافق الصناعية الخاصة.</li>
            </ul>
            <h3>معايير الجودة</h3>
            <p>يطبق قسم البناء والإنشاء في إيمبيريا معايير دولية مثل ISO 9001 و ISO 14001 والمعايير الوطنية الإيرانية في جميع المشاريع. ويتم إجراء مراقبة الجودة بشكل مستمر طوال مراحل التصميم، وشراء المواد، والتنفيذ، والتسليم، وتخضع كل مشروع لعدة فحوصات قبل التسليم.</p>
            <h3>الابتكار في البناء</h3>
            <p>يُعد استخدام نمذجة معلومات المباني (BIM)، والهياكل مسبقة الصنع وشبه مسبقة الصنع، والمواد الخفيفة والصديقة للبيئة، وأنظمة المباني الذكية والطاقات المتجددة، من بين الاستراتيجيات الرئيسية لإيمبيريا للنهوض بتكنولوجيا البناء.</p>
            """;

        var faContent2 = """
            <h2>توسعه شهری و شهرسازی امپریا</h2>
            <p>توسعه شهری در امپریا با رویکردی فراگیر و پایدار دنبال می‌شود. معتقدیم شهر باید مکانی برای زندگی، کار، سرگرمی و تعاملات انسانی باشد و همه شهروندان بدون در نظر گرفتن وضعیت اقتصادی و اجتماعی باید از امکانات یکسان بهره‌مند شوند.</p>
            <h3>اصول طراحی شهری پایدار</h3>
            <ul>
            <li><strong>تراکم مناسب و کاربری مختلط:</strong> ترکیب مسکونی، تجاری و اداری برای کاهش سفرهای شهری.</li>
            <li><strong>حمل‌ونقل پایدار:</strong> توسعه حمل‌ونقل عمومی، دوچرخه‌سواری و مسیرهای پیاده‌رو.</li>
            <li><strong>فضاهای سبز:</strong> ایجاد پارک‌ها، میدان‌ها و فضاهای باز عمومی برای کیفیت زندگی بهتر.</li>
            <li><strong>ترویج معماری محلی:</strong> الهام از الگوهای بومی و آب‌وهوایی منطقه در طراحی.</li>
            <li><strong>شهر متنوع و فراگیر:</strong> امکانات عمومی برای همه گروه‌های سنی و اجتماعی در دسترس باشد.</li>
            </ul>
            <h3>پروژه‌های نمونه شهری امپریا</h3>
            <p>امپریا در حال طراحی و اجرای چندین پروژه توسعه شهری مقیاس‌پذیر است. این پروژه‌ها شامل شهرک‌های جدید با کاربری مختلط، محوطه‌های تجاری تفریحی، باغ‌های فناور و شهرهای کوچک دانشی هستند که با تأکید بر محیط‌زیست پایدار، توسعه شهری مدرن را در کشور به تصویر می‌کشند.</p>
            <h3>آینده شهرسازی در ایران</h3>
            <p>با توجه به رشد سریع شهرنشینی در ایران، توسعه شهری هوشمند، مقاوم در برابر بلایا، کم‌مصرف انرژی و دارای کیفیت زندگی بالا باید اولویت اصلی سیاست‌گذاران شهری باشد. امپریا با تکیه بر دانش و تجربه خود می‌تواند در این مسیر نقش رهبری را ایفا کند.</p>
            """;

        var enContent2 = """
            <h2>Urban Development and City Planning at Imperia</h2>
            <p>Urban development at Imperia is pursued with a comprehensive and sustainable approach. We believe a city should be a place for living, work, entertainment and human interaction, and all citizens—regardless of economic and social status—should enjoy equal facilities.</p>
            <h3>Principles of Sustainable Urban Design</h3>
            <ul>
            <li><strong>Appropriate Density and Mixed-Use:</strong> Combining residential, commercial and office uses to reduce urban trips.</li>
            <li><strong>Sustainable Transportation:</strong> Developing public transit, cycling infrastructure and pedestrian paths.</li>
            <li><strong>Green Spaces:</strong> Creating parks, squares and public open spaces for better quality of life.</li>
            <li><strong>Promoting Local Architecture:</strong> Drawing inspiration from regional vernacular patterns and climate in design.</li>
            <li><strong>Diverse and Inclusive City:</strong> Public facilities accessible to all age and social groups.</li>
            </ul>
            <h3>Imperia's Flagship Urban Projects</h3>
            <p>Imperia is designing and implementing several scalable urban development projects. These include new mixed-use towns, commercial entertainment districts, technology parks and small university cities, showcasing modern urban development in the country with emphasis on environmental sustainability.</p>
            <h3>The Future of Urban Planning in Iran</h3>
            <p>Given the rapid urbanization growth in Iran, smart urban development that is disaster-resilient, energy-efficient and offers high quality of life must be the primary priority for urban policymakers. Imperia can play a leading role in this path by relying on its knowledge and experience.</p>
            """;

        var arContent2 = """
            <h2>التنمية الحضرية وتخطيط المدن في إيمبيريا</h2>
            <p>يُتابع التنمية الحضرية في إيمبيريا بنهج شامل ومستدام. ونؤمن بأن المدينة يجب أن تكون مكاناً للعيش والعمل والترفيه والتفاعل البشري، وأن يتمتع جميع المواطنين - بغض النظر عن وضعهم الاقتصادي والاجتماعي - بالمرافق المتساوية.</p>
            <h3>مبادئ التصميم الحضري المستدام</h3>
            <ul>
            <li><strong>الكثافة المناسبة والاستخدام المختلط:</strong> الجمع بين الاستخدامات السكنية والتجارية والمكتبية لتقليل الرحلات الحضرية.</li>
            <li><strong>النقل المستدام:</strong> تطوير النقل العام وبنية التحتية لركوب الدراجات ومسارات المشاة.</li>
            <li><strong>المساحات الخضراء:</strong> إنشاء حدائق وساحات ومساحات عامة مفتوحة لتحسين جودة الحياة.</li>
            <li><strong>تشجيع العمارة المحلية:</strong> الاستلهام من الأنماط المحلية الإقليمية والمناخ في التصميم.</li>
            <li><strong>المدينة المتنوعة والشاملة:</strong> المرافق العامة متاحة لجميع الفئات العمرية والاجتماعية.</li>
            </ul>
            <h3>مشاريع إيمبيريا الحضرية الرائدة</h3>
            <p>تقوم إيمبيريا بتصميم وتنفيذ عدة مشاريع تنمية حضرية قابلة للتوسع. وتشمل هذه مدناً جديدة ذات استخدام مختلط، وأحياء تجارية ترفيهية، وحدائق تكنولوجية، ومدناً جامعية صغيرة، تعرض التنمية الحضرية الحديثة في البلاد مع التأكيد على الاستدامة البيئية.</p>
            <h3>مستقبل التخطيط الحضري في إيران</h3>
            <p>نظراً للنمو السريع للتحضر في إيران، يجب أن يكون التنمية الحضرية الذكية التي تتحمل الكوارث، والفعالة من حيث استهلاك الطاقة، والتي توفر جودة حياة عالية، هي الأولوية الأساسية لصانعي السياسات الحضرية. ويمكن لإيمبيريا أن تلعب دوراً رائداً في هذا المسار بالاعتماد على معرفتها وخبرتها.</p>
            """;

        var faContent3 = """
            <h2>پروژه‌های بزرگ و غول‌پیکر امپریا</h2>
            <p>امپریا با دارا بودن ظرفیت مالی، فنی و اجرایی متشابه، پروژه‌های بزرگ و استراتژیک کشور را در اولویت کار خود قرار داده است. اجرای پروژه‌های غول‌پیکر نیازمند مدیریت حرفه‌ای ریسک، تأمین منابع مالی، هماهنگی چندجانبه و رعایت استانداردهای کیفی در سطح جهانی است.</p>
            <h3>انواع پروژه‌های بزرگ امپریا</h3>
            <ul>
            <li><strong>سدها و نیروگاه‌های برق‌آبی:</strong> تأمین آب کشاورزی و برق پایدار برای استان‌ها.</li>
            <li><strong>فرودگاه‌ها و بندرها:</strong> توسعه حمل‌ونقل هوایی و دریایی در نقاط کلیدی کشور.</li>
            <li><strong>راه‌آهن و بزرگراه‌ها:</strong> احداث خطوط راه‌آهن سرعتی و بزرگراه‌های ارتباطی محوری.</li>
            <li><strong>شهرهای جدید:</strong> برنامه‌ریزی و احداث شهرهای جدید جمعیتی برای کاهش فشار شهرهای بزرگ.</li>
            <li><strong>مراکز علمی و فناوری بزرگ:</strong> احداث شهرهای دانش و پارک‌های علم و فناوری مقیاس‌پذیر.</li>
            </ul>
            <h3>مدیریت پروژه‌های بزرگ</h3>
            <p>مدیریت پروژه‌های بزرگ در امپریا با استفاده از متدولوژی‌های روز دنیا شامل Agile، PMBOK و Earned Value Management انجام می‌شود. تیم‌های چند‌رشته‌ای در ریسک، تأمین مالی، برنامه‌ریزی، کنترل کیفی و روابط عمومی همکاری تنگاتنگی با هم دارند تا پروژه‌ها با کیفیت، در زمان مقرر و با هزینه کنترل‌شده به پایان برسند.</p>
            <h3>سهم پروژه‌های بزرگ در اقتصاد</h3>
            <p>اجرای پروژه‌های بزرگ علاوه بر ایجاد زیرساخت‌های کلیدی، به طور مستقیم و غیرمستقیم صدها هزار شغل ایجاد کرده و صنایع وابسته مانند فولاد، سیمان، مصالح ساختمانی و ماشین‌آلات را تحریک می‌کند. این پروژه‌ها همچنین عامل مهمی در جذب سرمایه‌گذاری خارجی و انتقال فناوری محسوب می‌شوند.</p>
            """;

        var enContent3 = """
            <h2>Imperia's Mega and Giant Projects</h2>
            <p>With equivalent financial, technical and operational capacity, Imperia has prioritized the country's large and strategic projects. Implementing mega-projects requires professional risk management, resource financing, multilateral coordination and adherence to global quality standards.</p>
            <h3>Types of Imperia Mega Projects</h3>
            <ul>
            <li><strong>Dams and Hydropower Plants:</strong> Securing agricultural water and sustainable electricity for provinces.</li>
            <li><strong>Airports and Ports:</strong> Developing air and sea transportation at key points in the country.</li>
            <li><strong>Railways and Highways:</strong> Constructing high-speed rail lines and key connecting highways.</li>
            <li><strong>New Cities:</strong> Planning and constructing new population cities to reduce pressure on large cities.</li>
            <li><strong>Large Science and Technology Centers:</strong> Establishing scalable university towns and science and technology parks.</li>
            </ul>
            <h3>Mega Project Management</h3>
            <p>Mega project management at Imperia is conducted using world-class methodologies including Agile, PMBOK and Earned Value Management. Multidisciplinary teams in risk, finance, planning, quality control and public relations work closely together so projects are completed with quality, on schedule, and with controlled costs.</p>
            <h3>Contribution of Mega Projects to the Economy</h3>
            <p>Beyond creating critical infrastructure, executing large projects directly and indirectly generates hundreds of thousands of jobs and stimulates dependent industries such as steel, cement, building materials and machinery. These projects are also considered an important factor in attracting foreign investment and technology transfer.</p>
            """;

        var arContent3 = """
            <h2>مشاريع إيمبيريا الكبرى والهائلة</h2>
            <p>بفضل القدرات المالية والفنية والتشغيلية المتكافئة، وضعت إيمبيريا مشاريع البلاد الكبيرة والاستراتيجية في أولويات عملها. ويتطلب تنفيذ المشاريع الهائلة إدارة احترافية للمخاطر، وتمويل الموارد، والتنسيق متعدد الأطراف، والالتزام بمعايير الجودة العالمية.</p>
            <h3>أنواع مشاريع إيمبيريا الكبرى</h3>
            <ul>
            <li><strong>السدود ومحطات الطاقة الكهرومائية:</strong> تأمين المياه الزراعية والكهرباء المستدامة للمحافظات.</li>
            <li><strong>المطارات والموانئ:</strong> تطوير النقل الجوي والبحري في نقاط أساسية في البلاد.</li>
            <li><strong>السكك الحديدية والطرق السريعة:</strong> إنشاء خطوط سكك حديدية عالية السرعة والطرق السريعة الرئيسية.</li>
            <li><strong>المدن الجديدة:</strong> التخطيط وإنشاء مدن سكانية جديدة لتقليل الضغط على المدن الكبرى.</li>
            <li><strong>المراكز العلمية والتكنولوجية الكبرى:</strong> إنشاء مدن جامعية وحدائق علمية وتكنولوجية قابلة للتوسع.</li>
            </ul>
            <h3>إدارة المشاريع الكبرى</h3>
            <p>تُجرى إدارة المشاريع الكبرى في إيمبيريا باستخدام منهجيات عالمية المستوى بما في ذلك أجايل و PMBOK وإدارة القيمة المكتسبة. وتعمل فرق متعددة التخصصات في المخاطر والمالية والتخطيط ومراقبة الجودة والعلاقات العامة معاً بشكل وثيق حتى تكتمل المشاريع بجودة وفي المواعيد المحددة وتكاليف مضبوطة.</p>
            <h3>مساهمة المشاريع الكبرى في الاقتصاد</h3>
            <p>إلى جانب خلق البنية التحتية الحرجة، يؤدي تنفيذ المشاريع الكبرى بشكل مباشر وغير مباشر إلى توليد مئات الآلاف من فرص العمل وتحفيز الصناعات التابعة مثل الفولاذ والإسمنت ومواد البناء والآلات. وتعتبر هذه المشاريع أيضاً عاملاً مهماً في جذب الاستثمارات الأجنبية ونقل التكنولوجيا.</p>
            """;

        var faContent4 = """
            <h2>شهرهای هوشمند در سبد پروژه‌های امپریا</h2>
            <p>امپریا شهر هوشمند را به عنوان یکی از راهبردهای اصلی توسعه شهری خود تعریف کرده است. هدف از شهر هوشمند، استفاده از فناوری‌های اطلاعاتی و ارتباطی برای ارتقای کیفیت زندگی شهروندان، بهینه‌سازی هزینه‌ها، مدیریت بهتر منابع و ایجاد محیطی پویا و پایدار برای زندگی و کسب‌وکار است.</p>
            <h3>مؤلفه‌های اصلی شهر هوشمند</h3>
            <ul>
            <li><strong>حمل‌ونقل هوشمند:</strong> ترافیک پیشگویانه، سامانه‌های پرداخت الکترونیک، پارکینگ هوشمند و خودروهای برقی و خودران.</li>
            <li><strong>انرژی هوشمند:</strong> شبکه‌های هوشمند برق، اندازه‌گیری هوشمند مصرف و مدیریت بار در ساعات پیک.</li>
            <li><strong>آب و فاضلاب هوشمند:</strong> تشخیص نشتی در شبکه آب، تصفیه هوشمند فاضلاب و مصرف بهینه آب.</li>
            <li><strong>امنیت و نگهبانی:</strong> دوربین‌های هوشمند، تشخیص خودکار حوادث و واکنش سریع نیروی انتظامی.</li>
            <li><strong>خدمات شهری هوشمند:</strong> سامانه‌های شهری الکترونیک، درخواست خدمات آنلاین، دولت الکترونیک شهری.</li>
            <li><strong>سلامت شهری:</strong> مراکز درمانی هوشمند، سنسورهای محیطی برای کنترل آلودگی هوا و صدا.</li>
            </ul>
            <h3>پروژه‌های نمونه شهر هوشمند امپریا</h3>
            <p>امپریا در حال احداث محوطه‌های نمونه شهر هوشمند در چندین شهر بزرگ است. این پروژه‌ها با نصب سنسورهای هوشمند، شبکه‌های پرسرعت 5G، مرکز کنترل جامع شهری و اپلیکیشن‌های مردمی، تجربه زندگی مدرن را برای شهروندان فراهم می‌سازند.</p>
            <h3>فرصت‌های اقتصادی شهر هوشمند</h3>
            <p>بازار شهر هوشمند یکی از سریع‌ترین بازارهای در حال رشد در جهان است. استارتاپ‌های دانش‌بنیان ایرانی در حوزه IoT، پردازش داده‌های بزرگ و هوش مصنوعی می‌توانند از این فرصت برای عرضه فناوری‌های بومی در سطح ملی و بین‌المللی بهره‌مند شوند.</p>
            """;

        var enContent4 = """
            <h2>Smart Cities in Imperia's Project Portfolio</h2>
            <p>Imperia has defined the smart city as one of its core urban development strategies. The goal of a smart city is to use information and communication technologies to improve citizens' quality of life, optimize costs, better manage resources, and create a dynamic and sustainable environment for living and doing business.</p>
            <h3>Core Components of a Smart City</h3>
            <ul>
            <li><strong>Smart Mobility:</strong> Predictive traffic management, electronic payment systems, smart parking, and electric/autonomous vehicles.</li>
            <li><strong>Smart Energy:</strong> Smart power grids, smart metering and peak load management.</li>
            <li><strong>Smart Water and Wastewater:</strong> Network leak detection, smart wastewater treatment and optimal water consumption.</li>
            <li><strong>Security and Surveillance:</strong> Smart cameras, automated incident detection and rapid law enforcement response.</li>
            <li><strong>Smart Urban Services:</strong> Electronic municipal systems, online service requests, and city e-government apps.</li>
            <li><strong>Urban Health:</strong> Smart medical centers and environmental sensors to monitor air and noise pollution.</li>
            </ul>
            <h3>Imperia's Smart City Pilot Projects</h3>
            <p>Imperia is constructing smart city pilot districts in several major cities. These projects—equipped with smart sensors, high-speed 5G networks, a comprehensive urban control center, and citizen apps—will deliver the modern living experience for residents.</p>
            <h3>Smart City Economic Opportunities</h3>
            <p>The smart city market is one of the fastest-growing markets worldwide. Iranian knowledge-based startups in IoT, big data processing and artificial intelligence can leverage this opportunity to supply indigenous technologies at national and international levels.</p>
            """;

        var arContent4 = """
            <h2>المدن الذكية في محفظة مشاريع إيمبيريا</h2>
            <p>حددت إيمبيريا المدينة الذكية كأحد استراتيجياتها الأساسية للتنمية الحضرية. والهدف من المدينة الذكية هو استخدام تكنولوجيا المعلومات والاتصالات لتحسين جودة حياة المواطنين، وتحسين التكاليف، وإدارة الموارد بشكل أفضل، وخلق بيئة ديناميكية ومستدامة للعيش وممارسة الأعمال.</p>
            <h3>المكونات الأساسية للمدينة الذكية</h3>
            <ul>
            <li><strong>التنقل الذكي:</strong> إدارة حركة المرور التنبؤية، وأنظمة الدفع الإلكتروني، ومواقف السيارات الذكية، والمركبات الكهربائية/ذاتية القيادة.</li>
            <li><strong>الطاقة الذكية:</strong> شبكات الكهرباء الذكية والقياس الذكي وإدارة الحمل في فترات الذروة.</li>
            <li><strong>المياه والمياه العادمة الذكية:</strong> اكتشاف التسرب في الشبكة، والمعالجة الذكية للمياه العادمة والاستهلاك الأمثل للمياه.</li>
            <li><strong>الأمن والمراقبة:</strong> الكاميرات الذكية، والكشف الآلي للحوادث، والاستجابة السريعة لقوات إنفاذ القانون.</li>
            <li><strong>الخدمات البلدية الذكية:</strong> الأنظمة البلدية الإلكترونية، وطلب الخدمات عبر الإنترنت، وتطبيقات الحكومة الإلكترونية للمدينة.</li>
            <li><strong>الصحة الحضرية:</strong> المراكز الطبية الذكية وأجهزة استشعار بيئية لمراقبة تلوث الهواء والضوضاء.</li>
            </ul>
            <h3>مشاريع إيمبيريا التجريبية للمدن الذكية</h3>
            <p>تقوم إيمبيريا ببناء أحياء تجريبية للمدن الذكية في عدة مدن كبرى. وستوفر هذه المشاريع - المجهزة بأجهزة استشعار ذكية، وشبكات 5G عالية السرعة، ومركز تحضري شامل للمدينة، وتطبيقات للمواطنين - تجربة المعيشة الحديثة للسكان.</p>
            <h3>الفرص الاقتصادية للمدينة الذكية</h3>
            <p>تُعد سوق المدن الذكية من أسرع الأسواق نمواً في العالم. ويمكن للشركات الناشئة الإيرانية القائمة على المعرفة في مجالات إنترنت الأشياء ومعالجة البيانات الضخمة والذكاء الاصطناعي الاستفادة من هذه الفرصة لتوريد التقنيات المحلية على المستويين الوطني والدولي.</p>
            """;

        var faContent5 = """
            <h2>بخش هتل و ریزورت امپریا</h2>
            <p>امپریا با تأسیس بزرگ‌ترین زنجیره هتل‌های لوکس و ریزورت‌های تفریحی در نقاط گردشگری کلیدی ایران، نقش مؤثری در ارتقای صنعت گردشگری و هسپیتالیتی کشور ایفا می‌کند. این پروژه‌ها با بهره‌گیری از معماری مطلع، خدمات رده‌بندی بین‌المللی و مدیریت حرفه‌ای، آماده میزبانی از میهمانان داخلی و خارجی هستند.</p>
            <h3>انواع پروژه‌های هتل‌داری امپریا</h3>
            <ul>
            <li><strong>هتل‌های لوکس ۵ ستاره شهری:</strong> در قلب تهران، اصفهان، شیراز، مشهد و کیش.</li>
            <li><strong>ریزورت‌های ساحلی:</strong> در سواحل خلیج فارس و دریای خزر با دسترسی مستقیم به دریا.</li>
            <li><strong>ریزورت‌های کوهستانی:</strong> در مناطق دامنه‌های البرز و زاگرس با امکان اسکی و طبیعت‌گردی.</li>
            <li><strong>هتل‌های درمانی و سلامت:</strong> مجهز به امکانات اسپا، آب‌گرم، ماساژ و خدمات درمانی مکمل.</li>
            <li><strong>مجموعه‌های کنفرانسی:</strong> سالن‌های بزرگ همایش، نمایشگاه و اتاق‌های جلسه مجهز.</li>
            </ul>
            <h3>ویژگی‌های هتل‌های امپریا</h3>
            <p>تمامی هتل‌های امپریا دارای گواهی‌نامه‌های معتبر بین‌المللی و استاندارد ISO هستند. اتاق‌های هتل‌ها با طراحی لوکس، مبلمان با کیفیت و سیستم‌های کنترل هوشمند تجهیز شده‌اند. رستوران‌های داخل مجموعه منوهای متنوعی از غذاهای ایرانی، جهانی و فست‌فود سالم ارائه می‌دهند و امکاناتی مانند استخر، سالن ورزشی، کلوپ کودک و مرکز خرید نیز در دسترس مهمانان قرار دارد.</p>
            <h3>گردشگری و اشتغال‌زایی</h3>
            <p>بخش هتل‌داری امپریا صدها هزار شغل مستقیم و غیرمستقیم در سراسر کشور ایجاد کرده است. آموزش حرفه‌ای کارکنان، رعایت حقوق کارگران و ایجاد مسیر پیشرفت شغلی از اصول اصلی مدیریت منابع انسانی در این بخش است.</p>
            """;

        var enContent5 = """
            <h2>Imperia's Hotels & Resorts Division</h2>
            <p>By establishing Iran's largest chain of luxury hotels and recreational resorts in key tourist destinations, Imperia plays a significant role in upgrading the country's tourism and hospitality industry. These projects—leveraging informed architecture, international-class services and professional management—are ready to host domestic and foreign guests.</p>
            <h3>Types of Imperia Hospitality Projects</h3>
            <ul>
            <li><strong>5-Star Luxury Urban Hotels:</strong> In the heart of Tehran, Isfahan, Shiraz, Mashhad and Kish.</li>
            <li><strong>Coastal Resorts:</strong> On the shores of the Persian Gulf and Caspian Sea with direct sea access.</li>
            <li><strong>Mountain Resorts:</strong> In the foothills of the Alborz and Zagros ranges, offering skiing and nature tourism.</li>
            <li><strong>Wellness & Medical Hotels:</strong> Equipped with spa, thermal waters, massage and complementary therapy services.</li>
            <li><strong>Conference Complexes:</strong> Large convention halls, exhibition space and equipped meeting rooms.</li>
            </ul>
            <h3>Features of Imperia Hotels</h3>
            <p>All Imperia hotels hold valid international certificates and ISO standards. Hotel rooms are furnished with luxury design, quality furniture and smart control systems. In-house restaurants offer diverse menus of Iranian, international and healthy fast food, while amenities like pools, gyms, kids' clubs and shopping centers are also available to guests.</p>
            <h3>Tourism and Employment Generation</h3>
            <p>Imperia's hospitality division has generated hundreds of thousands of direct and indirect jobs throughout the country. Professional employee training, observance of workers' rights and career path development are core principles of human resource management in this sector.</p>
            """;

        var arContent5 = """
            <h2>قسمت الفنادق والمنتجعات في إيمبيريا</h2>
            <p>تلعب إيمبيريا، من خلال إنشاء أكبر سلسلة فنادق فاخرة ومنتجعات ترفيهية في إيران في الوجهات السياحية الرئيسية، دوراً بارزاً في ترقية صناعة السياحة والضيافة في البلاد. وهذه المشاريع - بفضل العمارة المستنيرة والخدمات ذات المستوى الدولي والإدارة المهنية - جاهزة لاستضافة الضيوف المحليين والأجانب.</p>
            <h3>أنواع مشاريع إيمبيريا للضيافة</h3>
            <ul>
            <li><strong>فنادق حضرية فاخرة من فئة 5 نجوم:</strong> في قلب طهران وأصبهان وشيراز ومشهد وكيش.</li>
            <li><strong>منتجعات ساحلية:</strong> على شواطئ الخليج الفارسي وبحر قزوين مع وصول مباشر إلى البحر.</li>
            <li><strong>منتجعات جبلية:</strong> في سفوح جبال الألبورز وزاغروس، وتوفر التزلج والسياحة الطبيعية.</li>
            <li><strong>فنادق العافية والعلاجية:</strong> مجهزة بالمنتجعات الصحية والمياه الحرارية والتدليك وخدمات العلاج التكميلي.</li>
            <li><strong>مجمعات المؤتمرات:</strong> قاعات مؤتمرات كبيرة ومساحات للمعارض وغرف اجتماعات مجهزة.</li>
            </ul>
            <h3>مزايا فنادق إيمبيريا</h3>
            <p>تحتوي جميع فنادق إيمبيريا على شهادات دولية صالحة ومعايير أيزو. والغرف الفندقية مفروشة بتصميم فاخر وأثاث عالي الجودة وأنظمة تحكم ذكية. وتقدم المطاعم الداخلية قوائم متنوعة من الأطعمة الإيرانية والعالمية والوجبات السريعة الصحية، كما تتوفر وسائل الراحة مثل المسابح والصالات الرياضية ونوادي الأطفال ومراكز التسوق للضيوف.</p>
            <h3>السياحة وتوليد فرص العمل</h3>
            <p>أحدث قسم الضيافة في إيمبيريا مئات الآلاف من فرص العمل المباشرة وغير المباشرة في جميع أنحاء البلاد. ويعتبر التدريب المهني للموظفين، واحترام حقوق العمال، وتطوير المسارات الوظيفية من المبادئ الأساسية لإدارة الموارد البشرية في هذا القطاع.</p>
            """;

        var faContent6 = """
            <h2>مراکز تجاری و خرید امپریا</h2>
            <p>مراکز خرید امپریا به عنوان مراکز تجاری، فرهنگی و اجتماعی نقشی کلیدی در زندگی روزمره شهروندان بازی می‌کنند. این مجموعه‌ها با گردآوری بهترین برندهای داخلی و خارجی، رستوران‌ها، کافه‌ها، سینماها، فضاهای بازی کودکان و سالن‌های فرهنگی، تجربه‌ای متفاوت از خرید و تفریح را به همراه دارند.</p>
            <h3>انواع مراکز تجاری امپریا</h3>
            <ul>
            <li><strong>مراکز خرید بزرگ شهری:</strong> با بیش از ۲۰۰ واحد تجاری در نقاط دسترسی‌پذیر شهرها.</li>
            <li><strong>مراکز تجاری لوکس:</strong> متمرکز بر برندهای بین‌المللی و کالاهای دسته اول و لوکس.</li>
            <li><strong>مراکز فروش مستقیم (اوتلت):</strong> فروش محصولات با تخفیف ویژه در بیرون از مرکز شهر.</li>
            <li><strong>مجموعه‌های چندمنظوره:</strong> ترکیب خرید، تفرج، غذاخوری، سرگرمی و خدمات اداری و فرهنگی.</li>
            </ul>
            <h3>خدمات ویژه در مراکز خرید امپریا</h3>
            <p>پارکینگ هوشمند و گسترده، سیستم اطلاعات گردش، سرویس تحویل درب منزل، منطقه بازی کودکان با نظارت، سالن‌های ویژه رویداد، خدمات بانکی و آTM، فضای ویژه پرستاری و مادر و کودک، دسترسی مناسب برای معلولان و سالمندان، سرویس بهداشتی و استراحتگاه‌های متعدد از مهم‌ترین امکانات رفاهی این مراکز است.</p>
            <h3>نقش فرهنگی و اجتماعی</h3>
            <p>امپریا معتقد است مراکز خرید نباید فقط جای خرید باشند. به همین دلیل در تمام مجموعه‌های تجاری خود فضاهایی برای برگزاری نمایشگاه‌ها، همایش‌های فرهنگی، کارگاه‌های آموزشی، کنسرت‌ها و رویدادهای اجتماعی در نظر گرفته شده است تا علاوه بر تجارت، به عرصه حضور فرهنگی و هنری هم تبدیل شوند.</p>
            """;

        var enContent6 = """
            <h2>Imperia Commercial & Shopping Centers</h2>
            <p>Imperia shopping centers play a key role in citizens' daily lives as commercial, cultural and social hubs. These complexes—hosting the best domestic and foreign brands, restaurants, cafes, cinemas, children's play areas and cultural halls—deliver a differentiated shopping and entertainment experience.</p>
            <h3>Types of Imperia Commercial Centers</h3>
            <ul>
            <li><strong>Large Urban Shopping Centers:</strong> With over 200 retail units at accessible city locations.</li>
            <li><strong>Luxury Retail Centers:</strong> Focused on international brands and premium/luxury goods.</li>
            <li><strong>Factory Outlet Centers:</strong> Discounted product sales outside city centers.</li>
            <li><strong>Mixed-Use Complexes:</strong> Combining shopping, recreation, dining, entertainment with office and cultural services.</li>
            </ul>
            <h3>Special Services at Imperia Malls</h3>
            <p>Smart and extensive parking, wayfinding systems, home delivery services, supervised kids' play zones, dedicated event halls, banking and ATM services, nursing and mother-child areas, disabled and elderly-friendly access, restrooms and numerous lounges are the most important amenities at these centers.</p>
            <h3>Cultural and Social Role</h3>
            <p>Imperia believes shopping centers should be more than just places to buy things. Accordingly, all our commercial complexes include dedicated spaces for exhibitions, cultural conferences, educational workshops, concerts and social events, turning them into venues for cultural and artistic presence alongside commerce.</p>
            """;

        var arContent6 = """
            <h2>مراكز إيمبيريا التجارية والتسوق</h2>
            <p>تلعب مراكز تسوق إيمبيريا دوراً رئيسياً في الحياة اليومية للمواطنين كمراكز تجارية وثقافية واجتماعية. وتقدم هذه المجمعات - التي تستضيف أفضل العلامات التجارية المحلية والأجنبية والمطاعم والمقاهي ودور السينما ومناطق ألعاب الأطفال والقاعات الثقافية - تجربة تسوق وترفيه متميزة.</p>
            <h3>أنواع مراكز إيمبيريا التجارية</h3>
            <ul>
            <li><strong>مراكز التسوق الحضرية الكبرى:</strong> التي تضم أكثر من 200 وحدة تجارية في مواقع يمكن الوصول إليها في المدن.</li>
            <li><strong>مراكز البيع بالتجزئة الفاخرة:</strong> التي تركز على العلامات التجارية الدولية والبضائع الممتازة والفاخرة.</li>
            <li><strong>مراكز مخارج المصانع:</strong> المبيعات المخفضة خارج مراكز المدن.</li>
            <li><strong>المجمعات متعددة الاستخدامات:</strong> الجمع بين التسوق والترفيه وتناول الطعام والتسلية مع الخدمات المكتبية والثقافية.</li>
            </ul>
            <h3>خدمات خاصة في مراكز إيمبيريا للتسوق</h3>
            <p>يُعد موقف السيارات الذكي والواسع، وأنظمة تحديد المسارات، وخدمات التوصيل إلى المنازل، ومناطق ألعاب الأطفال الخاضعة للإشراف، وقاعات مخصصة للفعاليات، والخدمات المصرفية وأجهزة الصراف الآلي، ومناطق التمريض للأمهات والأطفال، والوصول الملائم لذوي الاحتياجات الخاصة وكبار السن، ودورات المياه والعديد من الصالات، من أهم وسائل الراحة في هذه المراكز.</p>
            <h3>الدور الثقافي والاجتماعي</h3>
            <p>تعتقد إيمبيريا أن مراكز التسوق يجب أن تكون أكثر من مجرد أماكن للشراء. وبناءً على ذلك، تضم جميع مجمعاتنا التجارية مساحات مخصصة للمعارض والمؤتمرات الثقافية وورش العمل التعليمية والحفلات الموسيقية والفعاليات الاجتماعية، مما يحولها إلى أماكن للوجود الثقافي والفني إلى جانب التجارة.</p>
            """;

        var faContent7 = """
            <h2>زیرساخت امپریا: ستاره توسعه اقتصادی</h2>
            <p>توسعه زیرساخت‌ها همانند شریان حیاتی برای رشد اقتصادی و بهبود رفاه اجتماعی است. امپریا با سرمایه‌گذاری هنگفت در زیرساخت‌های حمل‌ونقل، انرژی، آب، مخابرات و ساختمان‌های اساسی، در واقع زیربنای توسعه پایدار کشور را محکم‌تر می‌کند.</p>
            <h3>حمل‌ونقل چندوجهی</h3>
            <ul>
            <li><strong>راه‌آهن:</strong> تکمیل شبکه راه‌آهن سرعتی تهران–اصفهان، تهران–مشهد و گسترش راه‌آهن باری و مسافربری.</li>
            <li><strong>جاده‌ای:</strong> ساخت بزرگراه‌های محوری، جاده‌های مابین شهری و نوسازی جاده‌های آسیب‌دیده.</li>
            <li><strong>هوایی:</strong> تکمیل و به‌روزرسانی فرودگاه‌های بین‌المللی و ساخت فرودگاه‌های جدید.</li>
            <li><strong>دریایی:</strong> توسعه بندرهای شمالی و جنوبی، عمودی‌سازی بندرها و افزایش ظرفیت انبارداری و بارگیری.</li>
            </ul>
            <h3>انرژی پایدار</h3>
            <p>امپریا در حوزه انرژی راهبردی دوگانه را دنبال می‌کند: احداث نیروگاه‌های مدرن گازی، هسته‌ای و برق‌آبی برای افزایش ظرفیت تولید برق، و همزمان سرمایه‌گذاری عظیم در نیروگاه‌های خورشیدی، بادی و گازی زیستی برای توسعه انرژی پاک و کاهش کربن.</p>
            <h3>آب و فاضلاب</h3>
            <p>مدیریت منابع آب، ساخت سدها و مخازن ذخیره‌سازی، انتقال آب بین‌حوزه‌ای، تصفیه فاضلاب و تصفیه آب برای مصارف کشاورزی و صنعتی از مهم‌ترین پروژه‌های زیرساختی امپریا در بخش آب و فاضلاب هستند. این پروژه‌ها با تأکید بر کاهش اتلاف آب و بهره‌وری مصرف آب برنامه‌ریزی می‌شوند.</p>
            """;

        var enContent7 = """
            <h2>Imperia Infrastructure: The Backbone of Economic Development</h2>
            <p>Infrastructure development is the vital artery for economic growth and improved social welfare. By investing massively in transportation, energy, water, telecommunications and essential structures, Imperia reinforces the very foundation of the country's sustainable development.</p>
            <h3>Multimodal Transportation</h3>
            <ul>
            <li><strong>Railways:</strong> Completing Tehran-Isfahan and Tehran-Mashhad high-speed rail networks and expanding freight and passenger rail lines.</li>
            <li><strong>Roads:</strong> Building key highways, intercity roads and rehabilitating damaged roadways.</li>
            <li><strong>Airports:</strong> Completing and upgrading international airports and constructing new airports.</li>
            <li><strong>Maritime:</strong> Developing northern and southern ports, deepening port facilities and increasing storage and loading capacity.</li>
            </ul>
            <h3>Sustainable Energy</h3>
            <p>Imperia pursues a two-pronged strategy in the energy sector: building modern gas-fired, nuclear and hydropower plants to boost electricity generation capacity, while simultaneously investing heavily in solar, wind and biogas power stations for clean energy expansion and carbon reduction.</p>
            <h3>Water and Wastewater</h3>
            <p>Water resource management, dam and storage reservoir construction, inter-basin water transfer, wastewater treatment and water recycling for agricultural and industrial uses are among Imperia's most important infrastructure projects in the water and wastewater sector. These projects are planned with emphasis on reducing water loss and improving consumption efficiency.</p>
            """;

        var arContent7 = """
            <h2>بنية إيمبيريا التحتية: عمود الفقار للتنمية الاقتصادية</h2>
            <p>إن تطوير البنية التحتية هو الشريان الحيوي للنمو الاقتصادي وتحسين الرفاه الاجتماعي. ومن خلال الاستثمار بشكل هائل في النقل والطاقة والمياه والاتصالات والهياكل الأساسية، تعزز إيمبيريا الأساس نفسه للتنمية المستدامة في البلاد.</p>
            <h3>النقل متعدد الوسائط</h3>
            <ul>
            <li><strong>السكك الحديدية:</strong> إكمال شبكات السكك الحديدية عالية السرعة بين طهران وأصبهان وطهران ومشهد، وتوسيع خطوط السكك الحديدية للبضائع والركاب.</li>
            <li><strong>الطرق:</strong> بناء الطرق السريعة الرئيسية والطرق البينية بين المدن وتأهيل الطرق المتضررة.</li>
            <li><strong>المطارات:</strong> إكمال وترقية المطارات الدولية وإنشاء مطارات جديدة.</li>
            <li><strong>البحري:</strong> تطوير الموانئ الشمالية والجنوبية، وتعميق المرافق المرفئية، وزيادة قدرات التخزين والتحميل.</li>
            </ul>
            <h3>الطاقة المستدامة</h3>
            <p>تتبع إيمبيريا استراتيجية ذراعين في قطاع الطاقة: بناء محطات طاقة حديثة تعمل بالغاز والطاقة النووية والكهرومائية لزيادة قدرات توليد الكهرباء، مع الاستثمار بشكل كبير في نفس الوقت في محطات الطاقة الشمسية والريحية والغاز الحيوي لتوسيع الطاقة النظيفة وخفض الكربون.</p>
            <h3>المياه والمياه العادمة</h3>
            <p>يُعد إدارة موارد المياه، وبناء السدود وخزانات التخزين، ونقل المياه بين الأحواض، ومعالجة مياه الصرف الصحي، وإعادة تدوير المياه للاستخدامات الزراعية والصناعية، من أهم مشاريع إيمبيريا للبنية التحتية في قطاع المياه والمياه العادمة. وتُخطط هذه المشاريع مع التأكيد على تقليل هدر المياه وتحسين كفاءة الاستهلاك.</p>
            """;

        var seedPostsDev = new (
            string FaTitle, string FaSlug, string FaDesc, string FaContent,
            string EnTitle, string EnSlug, string EnDesc, string EnContent,
            string ArTitle, string ArSlug, string ArDesc, string ArContent)[]
        {
            (
                "ساخت‌وساز",
                "sakht-o-saz",
                "واحد ساخت‌وساز امپریا با پروژه‌های بزرگ و کیفیت درجه یک",
                faContent1,
                "Construction",
                "construction",
                "Imperia's construction division with major projects and premium quality",
                enContent1,
                "البناء والإنشاء",
                "al-bina-wal-ansha",
                "قسم البناء والإنشاء في إيمبيريا بمشاريع كبرى وجودة فائقة",
                arContent1
            ),
            (
                "توسعه شهری",
                "tose'eh-ye-shahri",
                "راهکارهای امپریا برای شهرسازی پایدار و توسعه شهری مطلوب",
                faContent2,
                "Urban Development",
                "urban-development",
                "Imperia's solutions for sustainable urban planning and desired urban development",
                enContent2,
                "التنمية الحضرية",
                "al-tanmiyah-al-hadariyah",
                "حلول إيمبيريا للتخطيط الحضري المستدام والتنمية الحضرية المنشودة",
                arContent2
            ),
            (
                "پروژه‌های بزرگ",
                "barnamehha-ye-bozorg",
                "پروژه‌های غول‌پیکر و استراتژیک امپریا در سراسر ایران",
                faContent3,
                "Mega Projects",
                "mega-projects",
                "Imperia's giant and strategic mega projects across Iran",
                enContent3,
                "المشاريع الكبرى",
                "al-mashari-al-kubra",
                "مشاريع إيمبيريا العملاقة والاستراتيجية في جميع أنحاء إيران",
                arContent3
            ),
            (
                "شهرهای هوشمند",
                "shahrha-ye-houshmand",
                "شهرهای هوشمند امپریا با فناوری‌های روز و کیفیت زندگی بالا",
                faContent4,
                "Smart Cities",
                "smart-cities",
                "Imperia smart cities with modern technologies and high quality of life",
                enContent4,
                "المدن الذكية",
                "al-mudun-al-dhakiyah",
                "مدن إيمبيريا الذكية بتقنيات حديثة وجودة حياة عالية",
                arContent4
            ),
            (
                "هتل و ریزورت",
                "hotel-o-resort",
                "زنجیره هتل‌ها و ریزورت‌های لوکس امپریا در نقاط گردشگری",
                faContent5,
                "Hotels & Resorts",
                "hotels-and-resorts",
                "Imperia's chain of luxury hotels and resorts at tourist destinations",
                enContent5,
                "الفنادق والمنتجعات",
                "al-fanadiq-wal-muntaja'at",
                "سلسلة فنادق ومنتجعات فاخرة تابعة لإيمبيريا في الوجهات السياحية",
                arContent5
            ),
            (
                "مراکز تجاری",
                "marakez-e-tejari",
                "مراکز خرید و تجاری چندمنظوره امپریا با امکانات گسترده",
                faContent6,
                "Commercial Centers",
                "commercial-centers",
                "Imperia's multi-purpose shopping and commercial centers with extensive facilities",
                enContent6,
                "المراكز التجارية",
                "al-marakez-al-tijariyah",
                "مراكز التسوق والتجارية متعددة الاستخدامات التابعة لإيمبيريا بمرافق واسعة",
                arContent6
            ),
            (
                "زیرساخت",
                "zirsakht",
                "زیرساخت‌های کلیدی انرژی، آب، حمل‌ونقل و مخابرات امپریا",
                faContent7,
                "Infrastructure",
                "infrastructure",
                "Imperia's key infrastructure for energy, water, transport and telecom",
                enContent7,
                "البنية التحتية",
                "al-bunyah-al-tahtiyah",
                "البنية التحتية الأساسية لإيمبيريا في الطاقة والمياه والنقل والاتصالات",
                arContent7
            ),
        };

        var existingTranslationsDev = await db.BlogPostTranslations
            .Include(t => t.Post)
            .Where(t => t.LanguagePrefix == "fa")
            .ToDictionaryAsync(t => t.Slug, t => t, cancellationToken);

        foreach (var post in seedPostsDev)
        {
            Guid postId;
            if (existingTranslationsDev.TryGetValue(post.FaSlug, out var existingFaTr))
            {
                postId = existingFaTr.PostId;

                var faTr = await db.BlogPostTranslations
                    .FirstOrDefaultAsync(t => t.PostId == postId && t.LanguagePrefix == "fa", cancellationToken);
                var enTr = await db.BlogPostTranslations
                    .FirstOrDefaultAsync(t => t.PostId == postId && t.LanguagePrefix == "en", cancellationToken);
                var arTr = await db.BlogPostTranslations
                    .FirstOrDefaultAsync(t => t.PostId == postId && t.LanguagePrefix == "ar", cancellationToken);

                if (faTr is not null && (faTr.Content is null || faTr.Content.Length < 50))
                {
                    faTr.Update(post.FaTitle, post.FaSlug, post.FaTitle, post.FaDesc, post.FaContent, post.FaTitle, post.FaDesc);
                }
                if (enTr is not null && (enTr.Content is null || enTr.Content.Length < 50))
                {
                    enTr.Update(post.EnTitle, post.EnSlug, post.EnTitle, post.EnDesc, post.EnContent, post.EnTitle, post.EnDesc);
                }
                if (arTr is not null && (arTr.Content is null || arTr.Content.Length < 50))
                {
                    arTr.Update(post.ArTitle, post.ArSlug, post.ArTitle, post.ArDesc, post.ArContent, post.ArTitle, post.ArDesc);
                }

                var existingPost = existingFaTr.Post;
                if (existingPost is not null && (existingPost.Content is null || existingPost.Content.Length < 50))
                {
                    existingPost.Update(
                        post.FaTitle, post.FaSlug, post.FaTitle, post.FaDesc, post.FaContent,
                        post.FaTitle, post.FaDesc, BlogPostStatus.Published, true);
                }

                await db.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Updated content for existing post: {FaTitle} ({EnTitle})", post.FaTitle, post.EnTitle);
                continue;
            }

            var slugTaken = await db.BlogPostTranslations.AnyAsync(
                t => (t.LanguagePrefix == "en" && t.Slug == post.EnSlug)
                     || (t.LanguagePrefix == "ar" && t.Slug == post.ArSlug),
                cancellationToken);
            if (slugTaken)
            {
                logger.LogWarning(
                    "Skipping seed post {FaSlug}: EN/AR slug already exists ({EnSlug}/{ArSlug}).",
                    post.FaSlug, post.EnSlug, post.ArSlug);
                continue;
            }

            var blogPost = BlogPost.Create(
                title: post.FaTitle,
                slug: post.FaSlug,
                keyword: post.FaTitle,
                description: post.FaDesc,
                content: post.FaContent,
                metaTitle: post.FaTitle,
                metaDescription: post.FaDesc,
                status: BlogPostStatus.Published,
                commentsEnabled: true);
            db.BlogPosts.Add(blogPost);
            await db.SaveChangesAsync(cancellationToken);
            postId = blogPost.Id;

            db.BlogPostTranslations.Add(BlogPostTranslation.Create(
                postId, "fa",
                post.FaTitle, post.FaSlug,
                post.FaTitle, post.FaDesc, post.FaContent, post.FaTitle, post.FaDesc));
            db.BlogPostTranslations.Add(BlogPostTranslation.Create(
                postId, "en",
                post.EnTitle, post.EnSlug,
                post.EnTitle, post.EnDesc, post.EnContent, post.EnTitle, post.EnDesc));
            db.BlogPostTranslations.Add(BlogPostTranslation.Create(
                postId, "ar",
                post.ArTitle, post.ArSlug,
                post.ArTitle, post.ArDesc, post.ArContent, post.ArTitle, post.ArDesc));

            db.BlogPostGroups.Add(BlogPostGroup.Create(postId, groupId));
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded blog post with content: {FaTitle} ({EnTitle})", post.FaTitle, post.EnTitle);
        }
    }

    private static async Task SeedImperiaDevelopmentMenuItemsAsync(
        ContentModulesDbContext db,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var menuGroup = await db.MenuGroups
            .FirstOrDefaultAsync(x => x.Key == "topmenu", cancellationToken);
        if (menuGroup is null)
        {
            logger.LogWarning("topmenu MenuGroup not found; skipping imperia dev menu seed.");
            return;
        }

        var blogGroupTr = await db.BlogGroupTranslations
            .FirstOrDefaultAsync(t => t.LanguagePrefix == "fa" && t.Slug == "imperia-tose'eh", cancellationToken);
        if (blogGroupTr is null)
        {
            logger.LogWarning("BlogGroup imperia-tose'eh not found; skipping imperia dev menu seed.");
            return;
        }

        Guid blogGroupId = blogGroupTr.GroupId;

        var postSlugsWithInfo = new (string FaSlug, string FaTitle, string EnTitle, string ArTitle, string EnSlug, string ArSlug)[]
        {
            ("sakht-o-saz", "ساخت‌وساز", "Construction", "البناء والإنشاء", "construction", "al-bina-wal-ansha"),
            ("tose'eh-ye-shahri", "توسعه شهری", "Urban Development", "التنمية الحضرية", "urban-development", "al-tanmiyah-al-hadariyah"),
            ("barnamehha-ye-bozorg", "پروژه‌های بزرگ", "Mega Projects", "المشاريع الكبرى", "mega-projects", "al-mashari-al-kubra"),
            ("shahrha-ye-houshmand", "شهرهای هوشمند", "Smart Cities", "المدن الذكية", "smart-cities", "al-mudun-al-dhakiyah"),
            ("hotel-o-resort", "هتل و ریزورت", "Hotels & Resorts", "الفنادق والمنتجعات", "hotels-and-resorts", "al-fanadiq-wal-muntaja'at"),
            ("marakez-e-tejari", "مراکز تجاری", "Commercial Centers", "المراكز التجارية", "commercial-centers", "al-marakez-al-tijariyah"),
            ("zirsakht", "زیرساخت", "Infrastructure", "البنية التحتية", "infrastructure", "al-bunyah-al-tahtiyah"),
        };

        var postTranslations = await db.BlogPostTranslations
            .Where(t => t.LanguagePrefix == "fa" && postSlugsWithInfo.Select(p => p.FaSlug).Contains(t.Slug))
            .ToListAsync(cancellationToken);
        var postIdsByFaSlug = postTranslations.ToDictionary(t => t.Slug, t => t.PostId);

        var parentMenuItem = await db.MenuItems
            .FirstOrDefaultAsync(x =>
                x.GroupId == menuGroup.Id &&
                (x.Function == MenuLinkFunction.GroupBlog && x.TargetId == blogGroupId ||
                 x.Url == "/development"),
                cancellationToken);

        Guid parentId;
        if (parentMenuItem is null)
        {
            var pItem = MenuItem.Create(
                title: "امپریا توسعه",
                url: "/development",
                groupId: menuGroup.Id,
                parentId: null,
                data: null,
                function: MenuLinkFunction.GroupBlog,
                targetId: blogGroupId,
                isMegaMenu: false,
                sortOrder: 12,
                isActive: true);
            db.MenuItems.Add(pItem);
            await db.SaveChangesAsync(cancellationToken);
            parentId = pItem.Id;

            db.MenuItemTranslations.Add(MenuItemTranslation.Create(parentId, "fa", "امپریا توسعه", "/development", null));
            db.MenuItemTranslations.Add(MenuItemTranslation.Create(parentId, "en", "IMPERIA DEVELOPMENT", "/development", null));
            db.MenuItemTranslations.Add(MenuItemTranslation.Create(parentId, "ar", "إيمبيريا للتنمية", "/development", null));
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded parent menu item for Imperia Development.");
        }
        else
        {
            parentId = parentMenuItem.Id;
            if (parentMenuItem.Function != MenuLinkFunction.GroupBlog || parentMenuItem.TargetId != blogGroupId || parentMenuItem.SortOrder != 12)
            {
                parentMenuItem.Update(
                    parentMenuItem.Title,
                    parentMenuItem.Url,
                    parentMenuItem.ParentId,
                    parentMenuItem.Data,
                    MenuLinkFunction.GroupBlog,
                    blogGroupId,
                    parentMenuItem.IsMegaMenu,
                    12,
                    parentMenuItem.IsActive);
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        int sortIndex = 0;
        foreach (var info in postSlugsWithInfo)
        {
            if (!postIdsByFaSlug.TryGetValue(info.FaSlug, out var postId))
            {
                logger.LogWarning("Post with fa slug {Slug} not found, skipping menu seed.", info.FaSlug);
                sortIndex++;
                continue;
            }

            var faUrl = $"/blog/{info.FaSlug}";
            var enUrl = $"/blog/{info.EnSlug}";
            var arUrl = $"/blog/{info.ArSlug}";

            var existingChild = await db.MenuItems.FirstOrDefaultAsync(x =>
                x.GroupId == menuGroup.Id &&
                x.ParentId == parentId &&
                x.Function == MenuLinkFunction.Post &&
                x.TargetId == postId,
                cancellationToken);

            if (existingChild is not null)
            {
                if (existingChild.SortOrder != sortIndex)
                {
                    existingChild.Update(
                        existingChild.Title,
                        existingChild.Url,
                        existingChild.ParentId,
                        existingChild.Data,
                        existingChild.Function,
                        existingChild.TargetId,
                        existingChild.IsMegaMenu,
                        sortIndex,
                        existingChild.IsActive);
                    await db.SaveChangesAsync(cancellationToken);
                }
                sortIndex++;
                continue;
            }

            var child = MenuItem.Create(
                title: info.FaTitle,
                url: faUrl,
                groupId: menuGroup.Id,
                parentId: parentId,
                data: null,
                function: MenuLinkFunction.Post,
                targetId: postId,
                isMegaMenu: false,
                sortOrder: sortIndex,
                isActive: true);
            db.MenuItems.Add(child);
            await db.SaveChangesAsync(cancellationToken);

            db.MenuItemTranslations.Add(MenuItemTranslation.Create(child.Id, "fa", info.FaTitle, faUrl, null));
            db.MenuItemTranslations.Add(MenuItemTranslation.Create(child.Id, "en", info.EnTitle, enUrl, null));
            db.MenuItemTranslations.Add(MenuItemTranslation.Create(child.Id, "ar", info.ArTitle, arUrl, null));
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded menu child: {FaTitle} under Imperia Development.", info.FaTitle);
            sortIndex++;
        }
    }
}
