using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bojan.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Replaces the English image-generation prompts still sitting in
    /// <c>ImageAlt</c> on databases seeded before they were corrected.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The seed file was fixed, but the seeder skips products entirely once the
    /// table has any rows — which is correct, it must not overwrite a shop's
    /// catalogue — so every database seeded before that fix still carries a
    /// paragraph of English studio-lighting direction where the product's name
    /// belongs. A screen reader announces it, image search indexes it, and when
    /// one of the remote images fails to load the browser paints the whole
    /// paragraph across the card.
    /// </para>
    /// <para>
    /// Only rows that are still entirely ASCII are touched. Persian text is not
    /// ASCII, so anything an operator has written — or anything a later seed
    /// wrote correctly — is left exactly as it is. A product with no alt at all
    /// gets one for the same reason.
    /// </para>
    /// <para>
    /// The replacement is the product's own title and its category, which is
    /// what the picture shows and what the corrected seed produces. Shorter than
    /// the seed's per-category phrasing, deliberately: this is repairing data
    /// rather than authoring it, and a title is the one description that is
    /// certainly true of every row.
    /// </para>
    /// </remarks>
    public partial class PersianImageAlt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE products AS p
                SET "ImageAlt" = p."Title" || '، ' || c."Name"
                FROM categories AS c
                WHERE c."Id" = p."CategoryId"
                  AND (p."ImageAlt" IS NULL OR p."ImageAlt" ~ '^[[:ascii:]]*$');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to undo. The previous values were prompts fed to an image
            // generator, not content anybody wrote, and they are not worth
            // keeping a copy of in order to restore.
        }
    }
}
