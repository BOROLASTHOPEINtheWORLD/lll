using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace labsupport.Models;

public partial class LabsupportContext : DbContext
{
    public LabsupportContext()
    {
    }

    public LabsupportContext(DbContextOptions<LabsupportContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Department> Departments { get; set; }

    public virtual DbSet<MainCategory> MainCategories { get; set; }

    public virtual DbSet<MessageAttachment> MessageAttachments { get; set; }

    public virtual DbSet<Position> Positions { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<SatisfactionRating> SatisfactionRatings { get; set; }

    public virtual DbSet<Subcategory> Subcategories { get; set; }

    public virtual DbSet<Ticket> Tickets { get; set; }

    public virtual DbSet<TicketAttachment> TicketAttachments { get; set; }

    public virtual DbSet<TicketComment> TicketComments { get; set; }

    public virtual DbSet<TicketDelegation> TicketDelegations { get; set; }

    public virtual DbSet<TicketHistory> TicketHistories { get; set; }

    public virtual DbSet<TicketStatus> TicketStatuses { get; set; }

    public virtual DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Department>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("departments_pkey");

            entity.ToTable("departments");

            entity.HasIndex(e => e.Name, "departments_name_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
        });

        modelBuilder.Entity<MainCategory>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("main_categories_pkey");

            entity.ToTable("main_categories");

            entity.HasIndex(e => e.Name, "main_categories_name_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
        });

        modelBuilder.Entity<MessageAttachment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("message_attachments_pkey");

            entity.ToTable("message_attachments");

            entity.HasIndex(e => e.CommentId, "idx_message_attachments_comment");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CommentId).HasColumnName("comment_id");
            entity.Property(e => e.FileName)
                .HasMaxLength(255)
                .HasColumnName("file_name");
            entity.Property(e => e.FilePath)
                .HasMaxLength(500)
                .HasColumnName("file_path");
            entity.Property(e => e.UploadedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("uploaded_at");

            entity.HasOne(d => d.Comment).WithMany(p => p.MessageAttachments)
                .HasForeignKey(d => d.CommentId)
                .HasConstraintName("message_attachments_comment_id_fkey");
        });

        modelBuilder.Entity<Position>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("positions_pkey");

            entity.ToTable("positions");

            entity.HasIndex(e => e.Name, "positions_name_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("roles_pkey");

            entity.ToTable("roles");

            entity.HasIndex(e => e.Name, "roles_name_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name)
                .HasMaxLength(50)
                .HasColumnName("name");
        });

        modelBuilder.Entity<SatisfactionRating>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("satisfaction_ratings_pkey");

            entity.ToTable("satisfaction_ratings");

            entity.HasIndex(e => e.TicketId, "idx_satisfaction_ticket");

            entity.HasIndex(e => e.TicketId, "satisfaction_ratings_ticket_id_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Comment).HasColumnName("comment");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.Rating).HasColumnName("rating");
            entity.Property(e => e.TicketId).HasColumnName("ticket_id");
            entity.Property(e => e.UserId)
                .ValueGeneratedOnAdd()
                .HasColumnName("user_id");

            entity.HasOne(d => d.Ticket).WithOne(p => p.SatisfactionRating)
                .HasForeignKey<SatisfactionRating>(d => d.TicketId)
                .HasConstraintName("satisfaction_ratings_ticket_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.SatisfactionRatings)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("satisfaction_ratings_user_id_fkey");
        });

        modelBuilder.Entity<Subcategory>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("subcategories_pkey");

            entity.ToTable("subcategories");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.MainCategoryId).HasColumnName("main_category_id");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");

            entity.HasOne(d => d.MainCategory).WithMany(p => p.Subcategories)
                .HasForeignKey(d => d.MainCategoryId)
                .HasConstraintName("subcategories_main_category_id_fkey");
        });

        modelBuilder.Entity<Ticket>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("tickets_pkey");

            entity.ToTable("tickets");

            entity.HasIndex(e => e.AssignedToId, "idx_tickets_assigned_to").HasFilter("(assigned_to_id IS NOT NULL)");

            entity.HasIndex(e => e.CreatedAt, "idx_tickets_created_at");

            entity.HasIndex(e => e.CreatedById, "idx_tickets_created_by");

            entity.HasIndex(e => e.StatusId, "idx_tickets_status");

            entity.HasIndex(e => e.TicketNumber, "tickets_ticket_number_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AssignedToId)
                .ValueGeneratedOnAdd()
                .HasColumnName("assigned_to_id");
            entity.Property(e => e.CategoryId).HasColumnName("category_id");
            entity.Property(e => e.ClosedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("closed_at");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedById)
                .ValueGeneratedOnAdd()
                .HasColumnName("created_by_id");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Priority).HasColumnName("priority");
            entity.Property(e => e.Resolution).HasColumnName("resolution");
            entity.Property(e => e.ResolvedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("resolved_at");
            entity.Property(e => e.StatusId).HasColumnName("status_id");
            entity.Property(e => e.TicketNumber)
                .HasMaxLength(20)
                .HasColumnName("ticket_number");
            entity.Property(e => e.Title)
                .HasMaxLength(200)
                .HasColumnName("title");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("updated_at");

            entity.Property(e => e.DueDate)
              .HasDefaultValueSql("CURRENT_TIMESTAMP")
              .HasColumnType("timestamp without time zone")
              .HasColumnName("due_date");

            entity.HasOne(d => d.AssignedTo).WithMany(p => p.TicketAssignedTos)
                .HasForeignKey(d => d.AssignedToId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("tickets_assigned_to_id_fkey");

            entity.HasOne(d => d.Category).WithMany(p => p.Tickets)
                .HasForeignKey(d => d.CategoryId)
                .HasConstraintName("tickets_category_id_fkey");

            entity.HasOne(d => d.CreatedBy).WithMany(p => p.TicketCreatedBies)
                .HasForeignKey(d => d.CreatedById)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("tickets_created_by_id_fkey");

            entity.HasOne(d => d.Status).WithMany(p => p.Tickets)
                .HasForeignKey(d => d.StatusId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("tickets_status_id_fkey");
        });

        modelBuilder.Entity<TicketAttachment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("ticket_attachments_pkey");

            entity.ToTable("ticket_attachments");

            entity.HasIndex(e => e.TicketId, "idx_attachments_ticket");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.FileName)
                .HasMaxLength(255)
                .HasColumnName("file_name");
            entity.Property(e => e.FilePath)
                .HasMaxLength(500)
                .HasColumnName("file_path");
            entity.Property(e => e.TicketId).HasColumnName("ticket_id");
            entity.Property(e => e.UploadedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("uploaded_at");

            entity.HasOne(d => d.Ticket).WithMany(p => p.TicketAttachments)
                .HasForeignKey(d => d.TicketId)
                .HasConstraintName("ticket_attachments_ticket_id_fkey");

        });

        modelBuilder.Entity<TicketComment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("ticket_comments_pkey");

            entity.ToTable("ticket_comments");

            entity.HasIndex(e => e.TicketId, "idx_comments_ticket");

            entity.HasIndex(e => e.UserId, "idx_comments_user");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.EditedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("edited_at");
            entity.Property(e => e.EditedById)
                .ValueGeneratedOnAdd()
                .HasColumnName("edited_by_id");
            entity.Property(e => e.IsInternal)
                .HasDefaultValue(false)
                .HasColumnName("is_internal");
            entity.Property(e => e.TicketId).HasColumnName("ticket_id");
            entity.Property(e => e.UserId)
                .ValueGeneratedOnAdd()
                .HasColumnName("user_id");

            entity.HasOne(d => d.EditedBy).WithMany(p => p.TicketCommentEditedBies)
                .HasForeignKey(d => d.EditedById)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("ticket_comments_edited_by_id_fkey");

            entity.HasOne(d => d.Ticket).WithMany(p => p.TicketComments)
                .HasForeignKey(d => d.TicketId)
                .HasConstraintName("ticket_comments_ticket_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.TicketCommentUsers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("ticket_comments_user_id_fkey");
        });

        modelBuilder.Entity<TicketDelegation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("ticket_delegations_pkey");

            entity.ToTable("ticket_delegations");

            entity.HasIndex(e => e.FromUserId, "idx_delegations_from_user");

            entity.HasIndex(e => e.TicketId, "idx_delegations_ticket");

            entity.HasIndex(e => e.ToUserId, "idx_delegations_to_user");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.FromUserId)
                .ValueGeneratedOnAdd()
                .HasColumnName("from_user_id");
            entity.Property(e => e.Reason).HasColumnName("reason");
            entity.Property(e => e.TicketId).HasColumnName("ticket_id");
            entity.Property(e => e.ToUserId)
                .ValueGeneratedOnAdd()
                .HasColumnName("to_user_id");

            entity.HasOne(d => d.FromUser).WithMany(p => p.TicketDelegationFromUsers)
                .HasForeignKey(d => d.FromUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("ticket_delegations_from_user_id_fkey");

            entity.HasOne(d => d.Ticket).WithMany(p => p.TicketDelegations)
                .HasForeignKey(d => d.TicketId)
                .HasConstraintName("ticket_delegations_ticket_id_fkey");

            entity.HasOne(d => d.ToUser).WithMany(p => p.TicketDelegationToUsers)
                .HasForeignKey(d => d.ToUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("ticket_delegations_to_user_id_fkey");
        });

        modelBuilder.Entity<TicketHistory>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("ticket_history_pkey");

            entity.ToTable("ticket_history");

            entity.HasIndex(e => e.TicketId, "idx_history_ticket");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ChangedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("changed_at");
            entity.Property(e => e.FieldName)
                .HasMaxLength(50)
                .HasColumnName("field_name");
            entity.Property(e => e.NewValue).HasColumnName("new_value");
            entity.Property(e => e.OldValue).HasColumnName("old_value");
            entity.Property(e => e.TicketId).HasColumnName("ticket_id");
            entity.Property(e => e.UserId)
                .ValueGeneratedOnAdd()
                .HasColumnName("user_id");

            entity.HasOne(d => d.Ticket).WithMany(p => p.TicketHistories)
                .HasForeignKey(d => d.TicketId)
                .HasConstraintName("ticket_history_ticket_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.TicketHistories)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("ticket_history_user_id_fkey");
        });

        modelBuilder.Entity<TicketStatus>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("ticket_statuses_pkey");

            entity.ToTable("ticket_statuses");

            entity.HasIndex(e => e.Name, "ticket_statuses_name_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name)
                .HasMaxLength(50)
                .HasColumnName("name");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("users_pkey");

            entity.ToTable("users");

            entity.HasIndex(e => e.DepartmentId, "idx_users_department");

            entity.HasIndex(e => e.Email, "idx_users_email");

            entity.HasIndex(e => e.RoleId, "idx_users_role");

            entity.HasIndex(e => e.Email, "users_email_key").IsUnique();


            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Username)
                .HasMaxLength(50) 
                .HasColumnName("username");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");
            entity.Property(e => e.DepartmentId).HasColumnName("department_id");
            entity.Property(e => e.Email)
                .HasMaxLength(100)
                .HasColumnName("email");
            entity.Property(e => e.AvatarPath)
                .HasMaxLength(100)
                .HasColumnName("avatar_path");
            entity.Property(e => e.FirstName)
                .HasMaxLength(50)
                .HasColumnName("first_name");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.LastLoginAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("last_login_at");
            entity.Property(e => e.LastName)
                .HasMaxLength(50)
                .HasColumnName("last_name");
            entity.Property(e => e.MiddleName)
                .HasMaxLength(50)
                .HasColumnName("middle_name");
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(256)
                .HasColumnName("password_hash");
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .HasColumnName("phone");
            entity.Property(e => e.PositionId).HasColumnName("position_id");
            entity.Property(e => e.RoleId).HasColumnName("role_id");

            entity.HasOne(d => d.Department).WithMany(p => p.Users)
                .HasForeignKey(d => d.DepartmentId)
                .HasConstraintName("users_department_id_fkey");

            entity.HasOne(d => d.Position).WithMany(p => p.Users)
                .HasForeignKey(d => d.PositionId)
                .HasConstraintName("users_position_id_fkey");

            entity.HasOne(d => d.Role).WithMany(p => p.Users)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("users_role_id_fkey");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
