using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "movies",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    poster_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    backdrop_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    age_rating = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    duration_minutes = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_movies", x => x.id);
                    table.CheckConstraint("ck_movies_duration_positive", "duration_minutes > 0");
                });

            migrationBuilder.CreateTable(
                name: "venues",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    capacity = table.Column<int>(type: "integer", nullable: false),
                    ticket_price = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_venues", x => x.id);
                    table.CheckConstraint("ck_venues_capacity_positive", "capacity > 0");
                    table.CheckConstraint("ck_venues_ticket_price_positive", "ticket_price > 0");
                });

            migrationBuilder.CreateTable(
                name: "events",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    movie_id = table.Column<int>(type: "integer", nullable: false),
                    venue_id = table.Column<int>(type: "integer", nullable: false),
                    show_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_events_movies_movie_id",
                        column: x => x.movie_id,
                        principalTable: "movies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_events_venues_venue_id",
                        column: x => x.venue_id,
                        principalTable: "venues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "venue_seats",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    venue_id = table.Column<int>(type: "integer", nullable: false),
                    row = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_venue_seats", x => x.id);
                    table.ForeignKey(
                        name: "fk_venue_seats_venues_venue_id",
                        column: x => x.venue_id,
                        principalTable: "venues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "bookings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    reference = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    event_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    payment_method = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bookings", x => x.id);
                    table.ForeignKey(
                        name: "fk_bookings_events_event_id",
                        column: x => x.event_id,
                        principalTable: "events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "seats",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    event_id = table.Column<int>(type: "integer", nullable: false),
                    venue_seat_id = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_seats", x => x.id);
                    table.CheckConstraint("ck_seats_status", "status IN ('Available', 'Booked')");
                    table.ForeignKey(
                        name: "fk_seats_events_event_id",
                        column: x => x.event_id,
                        principalTable: "events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_seats_venue_seats_venue_seat_id",
                        column: x => x.venue_seat_id,
                        principalTable: "venue_seats",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "booking_items",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    booking_id = table.Column<int>(type: "integer", nullable: false),
                    seat_id = table.Column<int>(type: "integer", nullable: false),
                    price = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_booking_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_booking_items_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_booking_items_seats_seat_id",
                        column: x => x.seat_id,
                        principalTable: "seats",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_booking_items_booking_id",
                table: "booking_items",
                column: "booking_id");

            migrationBuilder.CreateIndex(
                name: "ux_booking_items_one_booking_per_seat",
                table: "booking_items",
                column: "seat_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_bookings_event_id",
                table: "bookings",
                column: "event_id");

            migrationBuilder.CreateIndex(
                name: "ix_bookings_reference",
                table: "bookings",
                column: "reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_bookings_user_id_created_at",
                table: "bookings",
                columns: new[] { "user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_bookings_user_id_idempotency_key",
                table: "bookings",
                columns: new[] { "user_id", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_events_movie_id",
                table: "events",
                column: "movie_id");

            migrationBuilder.CreateIndex(
                name: "ix_events_venue_id_show_time",
                table: "events",
                columns: new[] { "venue_id", "show_time" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_movies_title",
                table: "movies",
                column: "title",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_seats_event_id_venue_seat_id",
                table: "seats",
                columns: new[] { "event_id", "venue_seat_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_seats_venue_seat_id",
                table: "seats",
                column: "venue_seat_id");

            migrationBuilder.CreateIndex(
                name: "ix_venue_seats_venue_id_row_number",
                table: "venue_seats",
                columns: new[] { "venue_id", "row", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_venues_name",
                table: "venues",
                column: "name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "booking_items");

            migrationBuilder.DropTable(
                name: "bookings");

            migrationBuilder.DropTable(
                name: "seats");

            migrationBuilder.DropTable(
                name: "events");

            migrationBuilder.DropTable(
                name: "venue_seats");

            migrationBuilder.DropTable(
                name: "movies");

            migrationBuilder.DropTable(
                name: "venues");
        }
    }
}
