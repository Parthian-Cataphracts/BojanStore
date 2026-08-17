using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bojan.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(BojanDbContext))]
    [Migration("20260817090000_PersianSearchFold")]
    public partial class PersianSearchFold : Migration
    {
        /// <summary>
        /// The SQL half of <c>PersianText.Fold</c>.
        /// </summary>
        /// <remarks>
        /// A search compares the folded needle against the folded column, and
        /// the column has to be folded by the database — folding it in memory
        /// would mean reading every row to find out which ones matched.
        ///
        /// One <c>translate()</c> does the whole job: the characters at the
        /// front of the first string are replaced by the character at the same
        /// position in the second, and the ones past the end of the second —
        /// the diacritics, the tatweel, the half-space and ordinary whitespace —
        /// are deleted, which is exactly the behaviour this needs.
        ///
        /// <c>IMMUTABLE</c> because it is: the same input always folds the same
        /// way. That is what lets an index be built on <c>bojan_fold(column)</c>
        /// if the catalogue ever grows enough to want one.
        /// </remarks>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION bojan_fold(input text)
                RETURNS text
                LANGUAGE sql
                IMMUTABLE
                PARALLEL SAFE
                RETURNS NULL ON NULL INPUT
                AS $$
                    SELECT translate(
                        lower(input),
                        'آأإٱيىئكةۀؤ۰۱۲۳۴۵۶۷۸۹٠١٢٣٤٥٦٧٨٩' || chr(1611) || chr(1612) || chr(1613) || chr(1614) || chr(1615) || chr(1616) || chr(1617) || chr(1618) || chr(1648) || 'ـ' || chr(8204) || chr(8205) || chr(32) || chr(9) || chr(13) || chr(10),
                        'اااایییکههو01234567890123456789'
                    )
                $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS bojan_fold(text);");
        }
    }
}
