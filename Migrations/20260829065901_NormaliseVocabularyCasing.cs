using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaArchive.Migrations
{
    /// <summary>
    /// Folds the genre and tag vocabularies to lower case — the canonical stored
    /// form the importer now writes; capitalisation is a rendering decision.
    /// Only these two: Person, Universe and Series names are proper nouns.
    ///
    /// Both tables have a unique index on Name, and migrations run at app
    /// startup, so a collision here would stop the app booting. Case-variant
    /// rows are therefore merged onto the lowest-Id survivor *before* anything
    /// is renamed.
    /// </summary>
    public partial class NormaliseVocabularyCasing : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            Normalise(migrationBuilder, "Genres", "MediaItemGenre", "GenreId");
            Normalise(migrationBuilder, "Tags", "MediaItemTag", "TagId");
        }

        private static void Normalise(MigrationBuilder migrationBuilder,
            string table, string joinTable, string foreignKey)
        {
            // Repoint every link at the survivor for its name. OR IGNORE covers
            // the case where the item already carries the survivor: the repoint
            // would duplicate the composite key, so it is skipped and the stale
            // row is swept up by the cascade below.
            migrationBuilder.Sql($"""
                UPDATE OR IGNORE {joinTable}
                SET {foreignKey} = (
                    SELECT MIN(survivor.Id) FROM {table} survivor
                    WHERE LOWER(survivor.Name) =
                        (SELECT LOWER(self.Name) FROM {table} self
                         WHERE self.Id = {joinTable}.{foreignKey})
                );
                """);

            // Drop the now-redundant rows; ON DELETE CASCADE clears any link
            // that OR IGNORE left pointing at them.
            migrationBuilder.Sql($"""
                DELETE FROM {table}
                WHERE Id NOT IN (SELECT MIN(Id) FROM {table} GROUP BY LOWER(Name));
                """);

            // SQLite's LOWER is ASCII-only, so a non-ASCII letter is left as-is
            // rather than mangled. The importer uses .NET's Unicode-aware
            // ToLowerInvariant; the two only diverge on non-ASCII terms.
            migrationBuilder.Sql($"""
                UPDATE {table} SET Name = LOWER(Name);
                """);
        }

        // Irreversible by nature: the original casing is not recorded anywhere,
        // and merged duplicates cannot be split back apart.
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
