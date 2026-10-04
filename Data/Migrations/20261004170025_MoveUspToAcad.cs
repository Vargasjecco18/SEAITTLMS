using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEAITT_LMS.Data.Migrations
{
    /// <summary>
    /// The USP strand belongs to the ACAD track (it was first put under TECH-PRO). Moves the strand and the people and
    /// enrollments that list it to track ACAD. Does nothing when USP does not exist or is not under TECH-PRO.
    ///
    /// A strand's track is repeated on students, teachers and enrollments, and the database checks that the pair
    /// (strand, track) is a real one, so the rows can't be changed one table at a time while that check is on:
    /// it is switched off for these updates and on again straight after.
    /// </summary>
    public partial class MoveUspToAcad : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) => MoveUsp(migrationBuilder, from: "TECH-PRO", to: "ACAD");

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) => MoveUsp(migrationBuilder, from: "ACAD", to: "TECH-PRO");

        private static void MoveUsp(MigrationBuilder migrationBuilder, string from, string to)
        {
            // The ids of the two tracks and of USP, read through a derived table (MySQL does not allow reading the table
            // that is being updated in a plain subquery).
            string Id(string table, string code) => $"(SELECT `id` FROM (SELECT `id` FROM `{table}` WHERE `code` = '{code}') AS `x`)";
            var oldTrack = Id("tracks", from);
            var newTrack = Id("tracks", to);
            var usp = Id("strands", "USP");

            // Only the rows that are on the old track, and only when the new track exists.
            var where = $"`id` > 0 AND `track_id` = {oldTrack} AND {newTrack} IS NOT NULL";

            migrationBuilder.Sql("SET FOREIGN_KEY_CHECKS = 0;");
            migrationBuilder.Sql($"UPDATE `students` SET `track_id` = {newTrack} WHERE {where} AND `strand_id` = {usp};");
            migrationBuilder.Sql($"UPDATE `teachers` SET `track_id` = {newTrack} WHERE {where} AND `strand_id` = {usp};");
            migrationBuilder.Sql($"UPDATE `enrollments` SET `track_id` = {newTrack} WHERE {where} AND `strand_id` = {usp};");
            migrationBuilder.Sql($"UPDATE `strands` SET `track_id` = {newTrack} WHERE {where} AND `code` = 'USP';");
            migrationBuilder.Sql("SET FOREIGN_KEY_CHECKS = 1;");
        }
    }
}
