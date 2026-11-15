using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Topogenia.Persistence.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "app_user",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    windows_login = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    login_normalized = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    display_name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    language = table.Column<string>(type: "TEXT", unicode: false, maxLength: 10, nullable: true),
                    is_admin = table.Column<bool>(type: "INTEGER", nullable: false),
                    first_visit_done = table.Column<bool>(type: "INTEGER", nullable: false),
                    last_project_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    updated_by = table.Column<Guid>(type: "TEXT", nullable: true),
                    version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_user", x => x.id);
                    table.CheckConstraint("CK_app_user_version", "\"version\" >= 1");
                    table.ForeignKey(
                        name: "FK_app_user_updated_by",
                        column: x => x.updated_by,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "schema_history",
                columns: table => new
                {
                    migration_id = table.Column<string>(type: "TEXT", unicode: false, maxLength: 150, nullable: false),
                    migration_fingerprint = table.Column<string>(type: "TEXT", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    schema_fingerprint = table.Column<string>(type: "TEXT", unicode: false, fixedLength: true, maxLength: 64, nullable: true),
                    application_version = table.Column<string>(type: "TEXT", unicode: false, maxLength: 32, nullable: false),
                    applied_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_schema_history", x => x.migration_id);
                });

            migrationBuilder.CreateTable(
                name: "audit_event",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    occurred_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    kind = table.Column<string>(type: "TEXT", unicode: false, maxLength: 60, nullable: false),
                    project_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    details = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_event", x => x.id);
                    table.ForeignKey(
                        name: "FK_audit_event_user",
                        column: x => x.user_id,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "project",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    code = table.Column<string>(type: "TEXT", unicode: false, maxLength: 6, nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    source_language = table.Column<string>(type: "TEXT", unicode: false, maxLength: 10, nullable: false),
                    description = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    shared_folder = table.Column<string>(type: "TEXT", maxLength: 400, nullable: true),
                    classification = table.Column<string>(type: "TEXT", unicode: false, maxLength: 24, nullable: false),
                    sharing_group_label = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    structure_version = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    created_by = table.Column<Guid>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    updated_by = table.Column<Guid>(type: "TEXT", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    deleted_by = table.Column<Guid>(type: "TEXT", nullable: true),
                    version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project", x => x.id);
                    table.CheckConstraint("CK_project_classification", "\"classification\" IN ('internal', 'confidential', 'strictly_confidential')");
                    table.CheckConstraint("CK_project_deletion", "((\"deleted_at\" IS NULL AND \"deleted_by\" IS NULL) OR (\"deleted_at\" IS NOT NULL AND \"deleted_by\" IS NOT NULL))");
                    table.CheckConstraint("CK_project_structure_version", "\"structure_version\" >= 0");
                    table.CheckConstraint("CK_project_version", "\"version\" >= 1");
                    table.ForeignKey(
                        name: "FK_project_created_by",
                        column: x => x.created_by,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_project_deleted_by",
                        column: x => x.deleted_by,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_project_updated_by",
                        column: x => x.updated_by,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "translation",
                columns: table => new
                {
                    object_type = table.Column<string>(type: "TEXT", unicode: false, maxLength: 10, nullable: false),
                    object_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    field = table.Column<string>(type: "TEXT", unicode: false, maxLength: 10, nullable: false),
                    language = table.Column<string>(type: "TEXT", unicode: false, maxLength: 10, nullable: false),
                    text = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    source = table.Column<string>(type: "TEXT", unicode: false, maxLength: 6, nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_by = table.Column<Guid>(type: "TEXT", nullable: true),
                    version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_translation", x => new { x.object_type, x.object_id, x.field, x.language });
                    table.CheckConstraint("CK_translation_object_field", "((\"object_type\" IN ('project', 'map', 'lane') AND \"field\" = 'name') OR (\"object_type\" IN ('step', 'flow') AND \"field\" = 'label'))");
                    table.CheckConstraint("CK_translation_source", "\"source\" IN ('auto', 'manual')");
                    table.CheckConstraint("CK_translation_version", "\"version\" >= 1");
                    table.ForeignKey(
                        name: "FK_translation_updated_by",
                        column: x => x.updated_by,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "deletion_batch",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    project_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    operation_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    deleted_by = table.Column<Guid>(type: "TEXT", nullable: false),
                    restored_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    restored_by = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deletion_batch", x => x.id);
                    table.CheckConstraint("CK_deletion_batch_restored", "((\"restored_at\" IS NULL AND \"restored_by\" IS NULL) OR (\"restored_at\" IS NOT NULL AND \"restored_by\" IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_deletion_batch_deleted_by",
                        column: x => x.deleted_by,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_deletion_batch_project",
                        column: x => x.project_id,
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_deletion_batch_restored_by",
                        column: x => x.restored_by,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "project_counter",
                columns: table => new
                {
                    project_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    counter = table.Column<string>(type: "TEXT", unicode: false, maxLength: 10, nullable: false),
                    last_value = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_counter", x => new { x.project_id, x.counter });
                    table.CheckConstraint("CK_project_counter_counter", "\"counter\" IN ('step', 'lane', 'pain_point', 'solution')");
                    table.CheckConstraint("CK_project_counter_last_value", "\"last_value\" >= 0");
                    table.ForeignKey(
                        name: "FK_project_counter_project",
                        column: x => x.project_id,
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "project_member",
                columns: table => new
                {
                    project_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    role = table.Column<string>(type: "TEXT", unicode: false, maxLength: 12, nullable: false),
                    is_sponsor = table.Column<bool>(type: "INTEGER", nullable: false),
                    is_key_contributor = table.Column<bool>(type: "INTEGER", nullable: false),
                    display_order = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    created_by = table.Column<Guid>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    updated_by = table.Column<Guid>(type: "TEXT", nullable: true),
                    version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_member", x => new { x.project_id, x.user_id });
                    table.CheckConstraint("CK_project_member_role", "\"role\" IN ('reader', 'contributor', 'facilitator')");
                    table.CheckConstraint("CK_project_member_version", "\"version\" >= 1");
                    table.ForeignKey(
                        name: "FK_project_member_created_by",
                        column: x => x.created_by,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_project_member_project",
                        column: x => x.project_id,
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_project_member_updated_by",
                        column: x => x.updated_by,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_project_member_user",
                        column: x => x.user_id,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "dissolution",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    map_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    step_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    deletion_batch_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    created_by = table.Column<Guid>(type: "TEXT", nullable: false),
                    closed_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    closed_by = table.Column<Guid>(type: "TEXT", nullable: true),
                    version = table.Column<int>(type: "INTEGER", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    updated_by = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dissolution", x => x.id);
                    table.CheckConstraint("CK_dissolution_closed", "((\"closed_at\" IS NULL AND \"closed_by\" IS NULL) OR (\"closed_at\" IS NOT NULL AND \"closed_by\" IS NOT NULL))");
                    table.CheckConstraint("CK_dissolution_version", "\"version\" >= 1");
                    table.ForeignKey(
                        name: "FK_dissolution_closed_by",
                        column: x => x.closed_by,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_dissolution_created_by",
                        column: x => x.created_by,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_dissolution_deletion_batch",
                        column: x => x.deletion_batch_id,
                        principalTable: "deletion_batch",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_dissolution_updated_by",
                        column: x => x.updated_by,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "dissolution_object",
                columns: table => new
                {
                    dissolution_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    object_type = table.Column<string>(type: "TEXT", unicode: false, maxLength: 10, nullable: false),
                    object_id = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dissolution_object", x => new { x.dissolution_id, x.object_type, x.object_id });
                    table.CheckConstraint("CK_dissolution_object_object_type", "\"object_type\" IN ('flow', 'lane')");
                    table.ForeignKey(
                        name: "FK_dissolution_object_dissolution",
                        column: x => x.dissolution_id,
                        principalTable: "dissolution",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "dissolution_step",
                columns: table => new
                {
                    dissolution_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    step_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    origin_lane_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    origin_column = table.Column<int>(type: "INTEGER", nullable: false),
                    origin_row = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dissolution_step", x => new { x.dissolution_id, x.step_id });
                    table.ForeignKey(
                        name: "FK_dissolution_step_dissolution",
                        column: x => x.dissolution_id,
                        principalTable: "dissolution",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "document",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    step_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    direction = table.Column<string>(type: "TEXT", unicode: false, maxLength: 6, nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    location = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    location_type = table.Column<string>(type: "TEXT", unicode: false, maxLength: 4, nullable: true),
                    wait_hours = table.Column<decimal>(type: "TEXT", precision: 9, scale: 2, nullable: true),
                    share_confirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    source_language = table.Column<string>(type: "TEXT", unicode: false, maxLength: 10, nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    created_by = table.Column<Guid>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    updated_by = table.Column<Guid>(type: "TEXT", nullable: true),
                    version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document", x => x.id);
                    table.CheckConstraint("CK_document_direction", "\"direction\" IN ('input', 'output')");
                    table.CheckConstraint("CK_document_location", "((\"location\" IS NULL AND \"location_type\" IS NULL) OR (\"location\" IS NOT NULL AND \"location_type\" IS NOT NULL))");
                    table.CheckConstraint("CK_document_location_type", "\"location_type\" IN ('url', 'path')");
                    table.CheckConstraint("CK_document_version", "\"version\" >= 1");
                    table.CheckConstraint("CK_document_wait_hours", "(\"wait_hours\" IS NULL OR (CAST(\"wait_hours\" AS REAL) >= 0 AND \"direction\" = 'input'))");
                    table.ForeignKey(
                        name: "FK_document_created_by",
                        column: x => x.created_by,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_document_updated_by",
                        column: x => x.updated_by,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "document_use",
                columns: table => new
                {
                    document_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    step_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    wait_hours = table.Column<decimal>(type: "TEXT", precision: 9, scale: 2, nullable: true),
                    percent_complete_accurate = table.Column<int>(type: "INTEGER", nullable: true),
                    pain_point_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    created_by = table.Column<Guid>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    updated_by = table.Column<Guid>(type: "TEXT", nullable: true),
                    version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_use", x => new { x.document_id, x.step_id });
                    table.CheckConstraint("CK_document_use_percent_complete_accurate", "\"percent_complete_accurate\" BETWEEN 0 AND 100");
                    table.CheckConstraint("CK_document_use_version", "\"version\" >= 1");
                    table.CheckConstraint("CK_document_use_wait_hours", "CAST(\"wait_hours\" AS REAL) >= 0");
                    table.ForeignKey(
                        name: "FK_document_use_created_by",
                        column: x => x.created_by,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_document_use_document",
                        column: x => x.document_id,
                        principalTable: "document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_document_use_updated_by",
                        column: x => x.updated_by,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "flow",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    map_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    source_step_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    target_step_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    label = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    created_by = table.Column<Guid>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    updated_by = table.Column<Guid>(type: "TEXT", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    deletion_batch_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_flow", x => x.id);
                    table.CheckConstraint("CK_flow_deletion", "((\"deleted_at\" IS NULL AND \"deletion_batch_id\" IS NULL) OR (\"deleted_at\" IS NOT NULL AND \"deletion_batch_id\" IS NOT NULL))");
                    table.CheckConstraint("CK_flow_not_reflexive", "\"source_step_id\" <> \"target_step_id\"");
                    table.CheckConstraint("CK_flow_version", "\"version\" >= 1");
                    table.ForeignKey(
                        name: "FK_flow_created_by",
                        column: x => x.created_by,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_flow_deletion_batch",
                        column: x => x.deletion_batch_id,
                        principalTable: "deletion_batch",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_flow_updated_by",
                        column: x => x.updated_by,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lane",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    map_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    code = table.Column<string>(type: "TEXT", unicode: false, maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    source_language = table.Column<string>(type: "TEXT", unicode: false, maxLength: 10, nullable: false),
                    lane_type = table.Column<string>(type: "TEXT", unicode: false, maxLength: 10, nullable: false),
                    actor_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    color = table.Column<string>(type: "TEXT", unicode: false, fixedLength: true, maxLength: 7, nullable: false),
                    sort_order = table.Column<int>(type: "INTEGER", nullable: false),
                    source_lane_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    created_by = table.Column<Guid>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    updated_by = table.Column<Guid>(type: "TEXT", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    deletion_batch_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lane", x => x.id);
                    table.UniqueConstraint("UQ_lane_id_map_id", x => new { x.id, x.map_id });
                    table.CheckConstraint("CK_lane_deletion", "((\"deleted_at\" IS NULL AND \"deletion_batch_id\" IS NULL) OR (\"deleted_at\" IS NOT NULL AND \"deletion_batch_id\" IS NOT NULL))");
                    table.CheckConstraint("CK_lane_lane_type", "\"lane_type\" IN ('person', 'department', 'system')");
                    table.CheckConstraint("CK_lane_version", "\"version\" >= 1");
                    table.ForeignKey(
                        name: "FK_lane_actor_user",
                        column: x => x.actor_user_id,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_lane_created_by",
                        column: x => x.created_by,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_lane_deletion_batch",
                        column: x => x.deletion_batch_id,
                        principalTable: "deletion_batch",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_lane_updated_by",
                        column: x => x.updated_by,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "operation_log",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    project_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    map_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    kind = table.Column<string>(type: "TEXT", unicode: false, maxLength: 60, nullable: false),
                    sequence = table.Column<int>(type: "INTEGER", nullable: false),
                    forward_json = table.Column<string>(type: "TEXT", nullable: false),
                    inverse_json = table.Column<string>(type: "TEXT", nullable: false),
                    touched = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    undone_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    redone_from_id = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operation_log", x => x.id);
                    table.CheckConstraint("CK_operation_log_sequence", "\"sequence\" >= 0");
                    table.ForeignKey(
                        name: "FK_operation_log_project",
                        column: x => x.project_id,
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_operation_log_user",
                        column: x => x.user_id,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pain_point",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    project_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    step_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    code = table.Column<string>(type: "TEXT", unicode: false, maxLength: 20, nullable: false),
                    description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    source_language = table.Column<string>(type: "TEXT", unicode: false, maxLength: 10, nullable: false),
                    lost_hours_per_month = table.Column<decimal>(type: "TEXT", precision: 6, scale: 1, nullable: true),
                    status = table.Column<string>(type: "TEXT", unicode: false, maxLength: 12, nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    created_by = table.Column<Guid>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    updated_by = table.Column<Guid>(type: "TEXT", nullable: true),
                    version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pain_point", x => x.id);
                    table.CheckConstraint("CK_pain_point_lost_hours_per_month", "CAST(\"lost_hours_per_month\" AS REAL) >= 0");
                    table.CheckConstraint("CK_pain_point_status", "\"status\" IN ('reported', 'scored', 'under_review', 'in_progress', 'implemented', 'adopted', 'dropped')");
                    table.CheckConstraint("CK_pain_point_version", "\"version\" >= 1");
                    table.ForeignKey(
                        name: "FK_pain_point_created_by",
                        column: x => x.created_by,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_pain_point_project",
                        column: x => x.project_id,
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_pain_point_updated_by",
                        column: x => x.updated_by,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pain_point_status_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    pain_point_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    status_before = table.Column<string>(type: "TEXT", unicode: false, maxLength: 12, nullable: true),
                    status_after = table.Column<string>(type: "TEXT", unicode: false, maxLength: 12, nullable: false),
                    comment = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    changed_by = table.Column<Guid>(type: "TEXT", nullable: false),
                    changed_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pain_point_status_history", x => x.id);
                    table.CheckConstraint("CK_pain_point_status_history_status_after", "\"status_after\" IN ('reported', 'scored', 'under_review', 'in_progress', 'implemented', 'adopted', 'dropped')");
                    table.CheckConstraint("CK_pain_point_status_history_status_before", "\"status_before\" IN ('reported', 'scored', 'under_review', 'in_progress', 'implemented', 'adopted', 'dropped')");
                    table.ForeignKey(
                        name: "FK_pain_point_status_history_changed_by",
                        column: x => x.changed_by,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_pain_point_status_history_pain_point",
                        column: x => x.pain_point_id,
                        principalTable: "pain_point",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "process_map",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    project_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    parent_step_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    source_language = table.Column<string>(type: "TEXT", unicode: false, maxLength: 10, nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    created_by = table.Column<Guid>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    updated_by = table.Column<Guid>(type: "TEXT", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    deletion_batch_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_process_map", x => x.id);
                    table.CheckConstraint("CK_process_map_deletion", "((\"deleted_at\" IS NULL AND \"deletion_batch_id\" IS NULL) OR (\"deleted_at\" IS NOT NULL AND \"deletion_batch_id\" IS NOT NULL))");
                    table.CheckConstraint("CK_process_map_root_alive", "(\"parent_step_id\" IS NOT NULL OR \"deleted_at\" IS NULL)");
                    table.CheckConstraint("CK_process_map_version", "\"version\" >= 1");
                    table.ForeignKey(
                        name: "FK_process_map_created_by",
                        column: x => x.created_by,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_process_map_deletion_batch",
                        column: x => x.deletion_batch_id,
                        principalTable: "deletion_batch",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_process_map_project",
                        column: x => x.project_id,
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_process_map_updated_by",
                        column: x => x.updated_by,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "step",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    map_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    lane_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    code = table.Column<string>(type: "TEXT", unicode: false, maxLength: 20, nullable: false),
                    label = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    source_language = table.Column<string>(type: "TEXT", unicode: false, maxLength: 10, nullable: false),
                    step_type = table.Column<string>(type: "TEXT", unicode: false, maxLength: 10, nullable: false),
                    column_index = table.Column<int>(type: "INTEGER", nullable: false),
                    row_index = table.Column<int>(type: "INTEGER", nullable: false),
                    assignee_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    automation_status = table.Column<string>(type: "TEXT", unicode: false, maxLength: 12, nullable: false),
                    origin_operation_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    created_by = table.Column<Guid>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    updated_by = table.Column<Guid>(type: "TEXT", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    deletion_batch_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_step", x => x.id);
                    table.CheckConstraint("CK_step_automation_status", "\"automation_status\" IN ('manual', 'in_progress', 'automated')");
                    table.CheckConstraint("CK_step_column_index", "\"column_index\" >= 0");
                    table.CheckConstraint("CK_step_deletion", "((\"deleted_at\" IS NULL AND \"deletion_batch_id\" IS NULL) OR (\"deleted_at\" IS NOT NULL AND \"deletion_batch_id\" IS NOT NULL))");
                    table.CheckConstraint("CK_step_row_index", "\"row_index\" BETWEEN 0 AND 50");
                    table.CheckConstraint("CK_step_step_type", "\"step_type\" IN ('task', 'decision', 'parallel', 'inclusive', 'wait', 'start', 'end')");
                    table.CheckConstraint("CK_step_version", "\"version\" >= 1");
                    table.ForeignKey(
                        name: "FK_step_assignee_user",
                        column: x => x.assignee_user_id,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_step_created_by",
                        column: x => x.created_by,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_step_deletion_batch",
                        column: x => x.deletion_batch_id,
                        principalTable: "deletion_batch",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_step_lane",
                        columns: x => new { x.lane_id, x.map_id },
                        principalTable: "lane",
                        principalColumns: new[] { "id", "map_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_step_map",
                        column: x => x.map_id,
                        principalTable: "process_map",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_step_updated_by",
                        column: x => x.updated_by,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "step_raci",
                columns: table => new
                {
                    step_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    lane_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    role = table.Column<string>(type: "TEXT", unicode: false, fixedLength: true, maxLength: 1, nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    created_by = table.Column<Guid>(type: "TEXT", nullable: false),
                    version = table.Column<int>(type: "INTEGER", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    updated_by = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_step_raci", x => new { x.step_id, x.lane_id });
                    table.CheckConstraint("CK_step_raci_role", "\"role\" IN ('A', 'S', 'C', 'I')");
                    table.CheckConstraint("CK_step_raci_version", "\"version\" >= 1");
                    table.ForeignKey(
                        name: "FK_step_raci_created_by",
                        column: x => x.created_by,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_step_raci_lane",
                        column: x => x.lane_id,
                        principalTable: "lane",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_step_raci_step",
                        column: x => x.step_id,
                        principalTable: "step",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_step_raci_updated_by",
                        column: x => x.updated_by,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "step_sheet",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    step_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    status = table.Column<string>(type: "TEXT", unicode: false, maxLength: 12, nullable: false),
                    actor = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    purpose = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    tools = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    frequency = table.Column<string>(type: "TEXT", unicode: false, maxLength: 16, nullable: true),
                    duration_minutes = table.Column<decimal>(type: "TEXT", precision: 9, scale: 2, nullable: true),
                    free_text = table.Column<string>(type: "TEXT", nullable: true),
                    source_language = table.Column<string>(type: "TEXT", unicode: false, maxLength: 10, nullable: false),
                    clarification_request = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    created_by = table.Column<Guid>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    updated_by = table.Column<Guid>(type: "TEXT", nullable: true),
                    version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_step_sheet", x => x.id);
                    table.CheckConstraint("CK_step_sheet_duration_minutes", "CAST(\"duration_minutes\" AS REAL) BETWEEN 0 AND 100000");
                    table.CheckConstraint("CK_step_sheet_frequency", "\"frequency\" IN ('daily', 'weekly', 'monthly', 'month_end_close', 'quarterly', 'yearly', 'occasional')");
                    table.CheckConstraint("CK_step_sheet_status", "\"status\" IN ('empty', 'draft', 'validated', 'needs_review')");
                    table.CheckConstraint("CK_step_sheet_version", "\"version\" >= 1");
                    table.ForeignKey(
                        name: "FK_step_sheet_created_by",
                        column: x => x.created_by,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_step_sheet_step",
                        column: x => x.step_id,
                        principalTable: "step",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_step_sheet_updated_by",
                        column: x => x.updated_by,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "step_operation",
                columns: table => new
                {
                    sheet_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    position = table.Column<int>(type: "INTEGER", nullable: false),
                    text = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    tool = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    nature = table.Column<string>(type: "TEXT", unicode: false, maxLength: 12, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_step_operation", x => new { x.sheet_id, x.position });
                    table.CheckConstraint("CK_step_operation_nature", "\"nature\" IN ('extract', 'copy_paste', 're_enter', 'format', 'calculate', 'check', 'chase', 'file', 'send', 'wait')");
                    table.CheckConstraint("CK_step_operation_position", "\"position\" BETWEEN 1 AND 50");
                    table.ForeignKey(
                        name: "FK_step_operation_sheet",
                        column: x => x.sheet_id,
                        principalTable: "step_sheet",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "step_sheet_version",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    sheet_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    version_number = table.Column<int>(type: "INTEGER", nullable: false),
                    content_json = table.Column<string>(type: "TEXT", nullable: false),
                    frequency = table.Column<string>(type: "TEXT", unicode: false, maxLength: 16, nullable: true),
                    duration_minutes = table.Column<decimal>(type: "TEXT", precision: 9, scale: 2, nullable: true),
                    validated_by = table.Column<Guid>(type: "TEXT", nullable: false),
                    validated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_step_sheet_version", x => x.id);
                    table.CheckConstraint("CK_step_sheet_version_duration_minutes", "CAST(\"duration_minutes\" AS REAL) BETWEEN 0 AND 100000");
                    table.CheckConstraint("CK_step_sheet_version_frequency", "\"frequency\" IN ('daily', 'weekly', 'monthly', 'month_end_close', 'quarterly', 'yearly', 'occasional')");
                    table.CheckConstraint("CK_step_sheet_version_version_number", "\"version_number\" >= 1");
                    table.ForeignKey(
                        name: "FK_step_sheet_version_sheet",
                        column: x => x.sheet_id,
                        principalTable: "step_sheet",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_step_sheet_version_validated_by",
                        column: x => x.validated_by,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UQ_app_user_login_normalized",
                table: "app_user",
                column: "login_normalized",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_audit_event_occurred_at",
                table: "audit_event",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "IX_audit_event_project_id",
                table: "audit_event",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_deletion_batch_project_id",
                table: "deletion_batch",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_dissolution_deletion_batch_id",
                table: "dissolution",
                column: "deletion_batch_id");

            migrationBuilder.CreateIndex(
                name: "IX_dissolution_step_id",
                table: "dissolution",
                column: "step_id");

            migrationBuilder.CreateIndex(
                name: "UX_dissolution_map_id_open",
                table: "dissolution",
                column: "map_id",
                unique: true,
                filter: "\"closed_at\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_dissolution_step_step_id",
                table: "dissolution_step",
                column: "step_id");

            migrationBuilder.CreateIndex(
                name: "IX_document_step_id_direction",
                table: "document",
                columns: new[] { "step_id", "direction" });

            migrationBuilder.CreateIndex(
                name: "IX_document_use_step_id",
                table: "document_use",
                column: "step_id");

            migrationBuilder.CreateIndex(
                name: "UX_document_use_pain_point_id",
                table: "document_use",
                column: "pain_point_id",
                unique: true,
                filter: "\"pain_point_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_flow_deletion_batch_id",
                table: "flow",
                column: "deletion_batch_id");

            migrationBuilder.CreateIndex(
                name: "IX_flow_map_id",
                table: "flow",
                column: "map_id");

            migrationBuilder.CreateIndex(
                name: "IX_flow_target_step_id",
                table: "flow",
                column: "target_step_id");

            migrationBuilder.CreateIndex(
                name: "UX_flow_source_step_id_target_step_id",
                table: "flow",
                columns: new[] { "source_step_id", "target_step_id" },
                unique: true,
                filter: "\"deleted_at\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_lane_deletion_batch_id",
                table: "lane",
                column: "deletion_batch_id");

            migrationBuilder.CreateIndex(
                name: "IX_lane_map_id",
                table: "lane",
                column: "map_id");

            migrationBuilder.CreateIndex(
                name: "UQ_lane_code",
                table: "lane",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_operation_log_map_id_user_id",
                table: "operation_log",
                columns: new[] { "map_id", "user_id" });

            migrationBuilder.CreateIndex(
                name: "IX_operation_log_project_id",
                table: "operation_log",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "UQ_operation_log_map_id_sequence",
                table: "operation_log",
                columns: new[] { "map_id", "sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pain_point_project_id",
                table: "pain_point",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_pain_point_step_id",
                table: "pain_point",
                column: "step_id");

            migrationBuilder.CreateIndex(
                name: "UQ_pain_point_code",
                table: "pain_point",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pain_point_status_history_pain_point_id_changed_at",
                table: "pain_point_status_history",
                columns: new[] { "pain_point_id", "changed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_process_map_deletion_batch_id",
                table: "process_map",
                column: "deletion_batch_id");

            migrationBuilder.CreateIndex(
                name: "IX_process_map_project_id",
                table: "process_map",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "UX_process_map_parent_step_id",
                table: "process_map",
                column: "parent_step_id",
                unique: true,
                filter: "(\"parent_step_id\" IS NOT NULL AND \"deleted_at\" IS NULL)");

            migrationBuilder.CreateIndex(
                name: "UX_process_map_project_id_root",
                table: "process_map",
                column: "project_id",
                unique: true,
                filter: "\"parent_step_id\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "UQ_project_code",
                table: "project",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_project_member_user_id",
                table: "project_member",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_step_assignee_user_id",
                table: "step",
                column: "assignee_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_step_deletion_batch_id",
                table: "step",
                column: "deletion_batch_id");

            migrationBuilder.CreateIndex(
                name: "IX_step_map_id",
                table: "step",
                column: "map_id");

            migrationBuilder.CreateIndex(
                name: "UQ_step_code",
                table: "step",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_step_lane_id_column_index_row_index",
                table: "step",
                columns: new[] { "lane_id", "column_index", "row_index" },
                unique: true,
                filter: "\"deleted_at\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_step_raci_lane_id",
                table: "step_raci",
                column: "lane_id");

            migrationBuilder.CreateIndex(
                name: "UX_step_raci_step_id_approver",
                table: "step_raci",
                column: "step_id",
                unique: true,
                filter: "\"role\" = 'A'");

            migrationBuilder.CreateIndex(
                name: "UQ_step_sheet_step_id",
                table: "step_sheet",
                column: "step_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_step_sheet_version_sheet_id_version_number",
                table: "step_sheet_version",
                columns: new[] { "sheet_id", "version_number" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_dissolution_map",
                table: "dissolution",
                column: "map_id",
                principalTable: "process_map",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_dissolution_step",
                table: "dissolution",
                column: "step_id",
                principalTable: "step",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_dissolution_step_step",
                table: "dissolution_step",
                column: "step_id",
                principalTable: "step",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_document_step",
                table: "document",
                column: "step_id",
                principalTable: "step",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_document_use_pain_point",
                table: "document_use",
                column: "pain_point_id",
                principalTable: "pain_point",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_document_use_step",
                table: "document_use",
                column: "step_id",
                principalTable: "step",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_flow_map",
                table: "flow",
                column: "map_id",
                principalTable: "process_map",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_flow_source_step",
                table: "flow",
                column: "source_step_id",
                principalTable: "step",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_flow_target_step",
                table: "flow",
                column: "target_step_id",
                principalTable: "step",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_lane_map",
                table: "lane",
                column: "map_id",
                principalTable: "process_map",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_operation_log_map",
                table: "operation_log",
                column: "map_id",
                principalTable: "process_map",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_pain_point_step",
                table: "pain_point",
                column: "step_id",
                principalTable: "step",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_process_map_parent_step",
                table: "process_map",
                column: "parent_step_id",
                principalTable: "step",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // SQLite cannot drop a foreign key (its provider refuses DropForeignKey, data M2): the tables are dropped with
            // their keys, each after the tables that refer to it. The references go round only through a detailed map
            // (process_map.parent_step_id): a file that holds one is refused by SQLite (FOREIGN KEY constraint failed) and
            // the transaction of the migration leaves it as it was.
            migrationBuilder.DropTable(
                name: "audit_event");

            migrationBuilder.DropTable(
                name: "dissolution_object");

            migrationBuilder.DropTable(
                name: "dissolution_step");

            migrationBuilder.DropTable(
                name: "document_use");

            migrationBuilder.DropTable(
                name: "flow");

            migrationBuilder.DropTable(
                name: "operation_log");

            migrationBuilder.DropTable(
                name: "pain_point_status_history");

            migrationBuilder.DropTable(
                name: "project_counter");

            migrationBuilder.DropTable(
                name: "project_member");

            migrationBuilder.DropTable(
                name: "schema_history");

            migrationBuilder.DropTable(
                name: "step_operation");

            migrationBuilder.DropTable(
                name: "step_raci");

            migrationBuilder.DropTable(
                name: "step_sheet_version");

            migrationBuilder.DropTable(
                name: "translation");

            migrationBuilder.DropTable(
                name: "dissolution");

            migrationBuilder.DropTable(
                name: "document");

            migrationBuilder.DropTable(
                name: "pain_point");

            migrationBuilder.DropTable(
                name: "step_sheet");

            migrationBuilder.DropTable(
                name: "step");

            migrationBuilder.DropTable(
                name: "lane");

            migrationBuilder.DropTable(
                name: "process_map");

            migrationBuilder.DropTable(
                name: "deletion_batch");

            migrationBuilder.DropTable(
                name: "project");

            migrationBuilder.DropTable(
                name: "app_user");
        }
    }
}
