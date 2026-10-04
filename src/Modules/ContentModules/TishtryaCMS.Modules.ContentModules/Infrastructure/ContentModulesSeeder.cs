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
        await SeedCivilizationAndHistoryMenuItemsAsync(db, logger, cancellationToken);
        await SeedPoliticsAndVisionMenuItemsAsync(db, logger, cancellationToken);
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
                    [show_timestamp] BIT NOT NULL CONSTRAINT [DF_BlogGroups_show_timestamp] DEFAULT (1),
                    CONSTRAINT [PK_BlogGroups] PRIMARY KEY ([id]),
                    CONSTRAINT [UQ_BlogGroups_slug] UNIQUE ([slug]),
                    CONSTRAINT [FK_BlogGroups_BlogGroups_parent_id]
                        FOREIGN KEY ([parent_id]) REFERENCES [content].[BlogGroups] ([id])
                );

                CREATE INDEX [IX_BlogGroups_parent_id] ON [content].[BlogGroups] ([parent_id]);
            END
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[content].[BlogGroups]', N'U') IS NOT NULL
               AND COL_LENGTH(N'content.BlogGroups', N'show_timestamp') IS NULL
            BEGIN
                ALTER TABLE [content].[BlogGroups]
                    ADD [show_timestamp] BIT NOT NULL
                        CONSTRAINT [DF_BlogGroups_show_timestamp] DEFAULT (1);
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
