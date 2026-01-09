using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace dotNet_Project.Migrations
{
    /// <inheritdoc />
    public partial class ManyToManyCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookmarks_Categories_CategoryId",
                table: "Bookmarks");

            migrationBuilder.DropIndex(
                name: "IX_Bookmarks_CategoryId",
                table: "Bookmarks");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "Bookmarks");

            migrationBuilder.CreateTable(
                name: "BookmarkCategory",
                columns: table => new
                {
                    BookmarksId = table.Column<int>(type: "int", nullable: false),
                    CategoriesId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookmarkCategory", x => new { x.BookmarksId, x.CategoriesId });
                    table.ForeignKey(
                        name: "FK_BookmarkCategory_Bookmarks_BookmarksId",
                        column: x => x.BookmarksId,
                        principalTable: "Bookmarks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BookmarkCategory_Categories_CategoriesId",
                        column: x => x.CategoriesId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BookmarkCategory_CategoriesId",
                table: "BookmarkCategory",
                column: "CategoriesId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookmarkCategory");

            migrationBuilder.AddColumn<int>(
                name: "CategoryId",
                table: "Bookmarks",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Bookmarks_CategoryId",
                table: "Bookmarks",
                column: "CategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookmarks_Categories_CategoryId",
                table: "Bookmarks",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
