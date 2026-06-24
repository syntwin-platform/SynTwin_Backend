using Microsoft.EntityFrameworkCore;
using Syntwin.Domain.Entities;
using Syntwin.Domain.Enums;

namespace Syntwin.Infrastructure.Persistence;

public sealed class SyntwinDbContext : DbContext
{
    public SyntwinDbContext(DbContextOptions<SyntwinDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<UserSubscription> UserSubscriptions => Set<UserSubscription>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<EmailOtp> EmailOtps => Set<EmailOtp>();
    public DbSet<Robot> Robots => Set<Robot>();
    public DbSet<RobotRuntimeSession> RobotRuntimeSessions => Set<RobotRuntimeSession>();
    public DbSet<RobotCommand> RobotCommands => Set<RobotCommand>();
    public DbSet<RobotSafetyPolicy> RobotSafetyPolicies => Set<RobotSafetyPolicy>();
    public DbSet<CommandResult> CommandResults => Set<CommandResult>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<RobotProgram> RobotPrograms => Set<RobotProgram>();
    public DbSet<RobotProgramStep> RobotProgramSteps => Set<RobotProgramStep>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<CompanyMember> CompanyMembers => Set<CompanyMember>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureUsers(modelBuilder);
        ConfigureRefreshTokens(modelBuilder);
        ConfigureSubscriptionPlans(modelBuilder);
        ConfigureUserSubscriptions(modelBuilder);
        ConfigurePaymentTransactions(modelBuilder);
        ConfigureEmailOtps(modelBuilder);
        ConfigureRobots(modelBuilder);
        ConfigureRobotSafetyPolicies(modelBuilder);
        ConfigureRobotRuntimeSessions(modelBuilder);
        ConfigureRobotCommands(modelBuilder);
        ConfigureCommandResults(modelBuilder);
        ConfigureRobotPrograms(modelBuilder);
        ConfigureRobotProgramSteps(modelBuilder);
        ConfigureAuditLogs(modelBuilder);
        ConfigureCompanies(modelBuilder);
        ConfigureCompanyMembers(modelBuilder);
    }

    private static void ConfigureCompanies(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Company>(entity =>
        {
            entity.ToTable("companies");

            entity.HasKey(company => company.Id);

            entity.Property(company => company.Name)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(company => company.Slug)
                .IsRequired()
                .HasMaxLength(180);

            entity.HasIndex(company => company.Slug)
                .IsUnique()
                .HasDatabaseName("UX_companies_slug");

            entity.Property(company => company.Industry)
                .HasMaxLength(100);

            entity.Property(company => company.Address)
                .HasMaxLength(300);

            entity.Property(company => company.Timezone)
                .IsRequired()
                .HasMaxLength(50)
                .HasDefaultValue("Asia/Ho_Chi_Minh");

            entity.Property(company => company.LogoUrl)
                .HasMaxLength(500);

            entity.Property(company => company.Status)
                .HasConversion<string>()
                .IsRequired()
                .HasMaxLength(30);

            entity.Property(company => company.CreatedAt)
                .IsRequired();

            entity.HasIndex(company => company.CreatedByUserId)
                .HasDatabaseName("IX_companies_created_by_user_id");

            entity.HasOne(company => company.CreatedByUser)
                .WithMany(user => user.CreatedCompanies)
                .HasForeignKey(company => company.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureCompanyMembers(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CompanyMember>(entity =>
        {
            entity.ToTable("company_members");

            entity.HasKey(member => new { member.CompanyId, member.UserId });

            entity.Property(member => member.Role)
                .HasConversion<string>()
                .IsRequired()
                .HasMaxLength(30);

            entity.Property(member => member.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            entity.Property(member => member.JoinedAt)
                .IsRequired();

            entity.HasIndex(member => member.UserId)
                .HasDatabaseName("IX_company_members_user_id");

            entity.HasIndex(member => new { member.CompanyId, member.IsActive })
                .HasDatabaseName("IX_company_members_company_active");

            entity.HasOne(member => member.Company)
                .WithMany(company => company.Members)
                .HasForeignKey(member => member.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(member => member.User)
                .WithMany(user => user.CompanyMemberships)
                .HasForeignKey(member => member.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureUsers(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");

            entity.HasKey(user => user.Id);

            entity.Property(user => user.Email)
                .IsRequired()
                .HasMaxLength(100);

            entity.HasIndex(user => user.Email)
                .IsUnique()
                .HasDatabaseName("UX_users_email");

            entity.Property(user => user.PasswordHash)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(user => user.Role)
                .HasConversion<string>()
                .IsRequired()
                .HasMaxLength(50);

            entity.HasIndex(user => user.Role)
                .HasDatabaseName("IX_users_role");

            entity.Property(user => user.Status)
                .HasConversion<string>()
                .IsRequired()
                .HasMaxLength(50);

            entity.HasIndex(user => user.Status)
                .HasDatabaseName("IX_users_status");

            entity.Property(user => user.Timezone)
                .IsRequired()
                .HasMaxLength(50)
                .HasDefaultValue("UTC");

            entity.Property(user => user.FullName)
                .HasMaxLength(100);

            entity.Property(user => user.AvatarUrl)
                .HasMaxLength(500);

            entity.Property(user => user.CreatedAt)
                .IsRequired();
        });
    }

    private static void ConfigureRefreshTokens(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("refresh_tokens");

            entity.HasKey(token => token.Id);

            entity.Property(token => token.TokenHash)
                .IsRequired()
                .HasMaxLength(255);

            entity.HasIndex(token => token.TokenHash)
                .IsUnique()
                .HasDatabaseName("UX_refresh_tokens_token_hash");

            entity.Property(token => token.JwtId)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(token => token.CreatedByIp)
                .HasMaxLength(45);

            entity.Property(token => token.ReplacedByTokenHash)
                .HasMaxLength(255);

            entity.Property(token => token.ExpiresAt)
                .IsRequired();

            entity.Property(token => token.CreatedAt)
                .IsRequired();

            entity.HasIndex(token => token.UserId)
                .HasDatabaseName("IX_refresh_tokens_user_id");

            entity.HasIndex(token => token.JwtId)
                .HasDatabaseName("IX_refresh_tokens_jwt_id");

            entity.HasIndex(token => token.ExpiresAt)
                .HasDatabaseName("IX_refresh_tokens_expires_at");

            entity.HasOne(token => token.User)
                .WithMany(user => user.RefreshTokens)
                .HasForeignKey(token => token.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureSubscriptionPlans(ModelBuilder modelBuilder)
    {
        var seedDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        modelBuilder.Entity<SubscriptionPlan>(entity =>
        {
            entity.ToTable("subscription_plans");

            entity.HasKey(plan => plan.Id);

            entity.Property(plan => plan.Code)
                .HasConversion<string>()
                .IsRequired()
                .HasMaxLength(50);

            entity.HasIndex(plan => plan.Code)
                .IsUnique()
                .HasDatabaseName("UX_subscription_plans_code");

            entity.Property(plan => plan.Name)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(plan => plan.MonthlyPrice)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            entity.Property(plan => plan.MaxRobots)
                .IsRequired();

            entity.Property(plan => plan.CanView3D)
                .IsRequired();

            entity.Property(plan => plan.CanSendCommand)
                .IsRequired();

            entity.Property(plan => plan.IsActive)
                .IsRequired();

            entity.Property(plan => plan.CreatedAt)
                .IsRequired();

            entity.HasData(
                new SubscriptionPlan
                {
                    Id = 1,
                    Code = SubscriptionPlanCode.Free,
                    Name = "Free",
                    MonthlyPrice = 0,
                    MaxRobots = 1,
                    CanView3D = false,
                    CanSendCommand = false,
                    AuditRetentionDays = 7,
                    IsActive = true,
                    CreatedAt = seedDate
                },
                new SubscriptionPlan
                {
                    Id = 2,
                    Code = SubscriptionPlanCode.Basic,
                    Name = "Basic",
                    MonthlyPrice = 99000,
                    MaxRobots = 3,
                    CanView3D = true,
                    CanSendCommand = false,
                    AuditRetentionDays = 30,
                    IsActive = true,
                    CreatedAt = seedDate
                },
                new SubscriptionPlan
                {
                    Id = 3,
                    Code = SubscriptionPlanCode.Premium,
                    Name = "Premium",
                    MonthlyPrice = 299000,
                    MaxRobots = 10,
                    CanView3D = true,
                    CanSendCommand = true,
                    AuditRetentionDays = 365,
                    IsActive = true,
                    CreatedAt = seedDate
                });
        });
    }

    private static void ConfigureUserSubscriptions(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserSubscription>(entity =>
        {
            entity.ToTable("user_subscriptions");

            entity.HasKey(subscription => subscription.Id);

            entity.Property(subscription => subscription.Status)
                .HasConversion<string>()
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(subscription => subscription.StartsAt)
                .IsRequired();

            entity.Property(subscription => subscription.AutoRenew)
                .IsRequired();

            entity.Property(subscription => subscription.CreatedAt)
                .IsRequired();

            entity.HasIndex(subscription => subscription.UserId)
                .HasDatabaseName("IX_user_subscriptions_user_id");

            entity.HasIndex(subscription => subscription.PlanId)
                .HasDatabaseName("IX_user_subscriptions_plan_id");

            entity.HasIndex(subscription => subscription.Status)
                .HasDatabaseName("IX_user_subscriptions_status");

            entity.HasIndex(subscription => new { subscription.UserId, subscription.Status })
                .HasDatabaseName("IX_user_subscriptions_user_status");

            entity.HasOne(subscription => subscription.User)
                .WithMany(user => user.Subscriptions)
                .HasForeignKey(subscription => subscription.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(subscription => subscription.Plan)
                .WithMany(plan => plan.UserSubscriptions)
                .HasForeignKey(subscription => subscription.PlanId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigurePaymentTransactions(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PaymentTransaction>(entity =>
        {
            entity.ToTable("payment_transactions");

            entity.HasKey(payment => payment.Id);

            entity.Property(payment => payment.Provider)
                .HasConversion<string>()
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(payment => payment.MerchantTransactionRef)
                .HasMaxLength(100);

            entity.Property(payment => payment.ProviderTransactionId)
                .HasMaxLength(150);

            entity.Property(payment => payment.Amount)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            entity.Property(payment => payment.Currency)
                .IsRequired()
                .HasMaxLength(10)
                .HasDefaultValue("VND");

            entity.Property(payment => payment.Status)
                .HasConversion<string>()
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(payment => payment.ResponseCode)
                .HasMaxLength(20);

            entity.Property(payment => payment.TransactionStatus)
                .HasMaxLength(20);

            entity.Property(payment => payment.BankCode)
                .HasMaxLength(50);

            entity.Property(payment => payment.FailureReason)
                .HasMaxLength(500);

            entity.Property(payment => payment.RawPayloadJson)
                .HasColumnType("nvarchar(max)");

            entity.Property(payment => payment.CreatedAt)
                .IsRequired();

            entity.HasIndex(payment => payment.UserId)
                .HasDatabaseName("IX_payment_transactions_user_id");

            entity.HasIndex(payment => payment.SubscriptionId)
                .HasDatabaseName("IX_payment_transactions_subscription_id");

            entity.HasIndex(payment => payment.ProviderTransactionId)
                .HasDatabaseName("IX_payment_transactions_provider_transaction_id");

            entity.HasIndex(payment => payment.MerchantTransactionRef)
                .IsUnique()
                .HasFilter("[MerchantTransactionRef] IS NOT NULL")
                .HasDatabaseName("UX_payment_transactions_merchant_transaction_ref");

            entity.HasIndex(payment => payment.Status)
                .HasDatabaseName("IX_payment_transactions_status");

            entity.HasOne(payment => payment.User)
                .WithMany(user => user.PaymentTransactions)
                .HasForeignKey(payment => payment.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(payment => payment.Subscription)
                .WithMany(subscription => subscription.PaymentTransactions)
                .HasForeignKey(payment => payment.SubscriptionId)
                .OnDelete(DeleteBehavior.SetNull);
        });

    }
    private static void ConfigureEmailOtps(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EmailOtp>(entity =>
        {
            entity.ToTable("email_otps");

            entity.HasKey(otp => otp.Id);

            entity.Property(otp => otp.Email)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(otp => otp.OtpHash)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(otp => otp.Purpose)
                .IsRequired()
                .HasMaxLength(50)
                .HasDefaultValue("LOGIN_CODE");

            entity.Property(otp => otp.ExpiresAt)
                .IsRequired();

            entity.Property(otp => otp.CreatedAt)
                .IsRequired();

            entity.HasIndex(otp => otp.Email)
                .HasDatabaseName("IX_email_otps_email");

            entity.HasIndex(otp => otp.UserId)
                .HasDatabaseName("IX_email_otps_user_id");

            entity.HasOne(otp => otp.User)
                .WithMany()
                .HasForeignKey(otp => otp.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.Property(otp => otp.AttemptCount)
    .IsRequired()
    .HasDefaultValue(0);

            entity.Property(otp => otp.MaxAttempts)
                .IsRequired()
                .HasDefaultValue(5);

            entity.HasIndex(otp => new { otp.Email, otp.Purpose, otp.ExpiresAt })
                .HasDatabaseName("IX_email_otps_email_purpose_expires_at");
        });
    }

    private static void ConfigureRobots(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Robot>(entity =>
        {
            entity.ToTable("robots");

            entity.HasKey(robot => robot.Id);

            entity.Property(robot => robot.RobotName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(robot => robot.Model)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(robot => robot.ConnectionType)
                .IsRequired()
                .HasMaxLength(50)
                .HasDefaultValue("HTTP");

            entity.Property(robot => robot.Status)
                .HasConversion<string>()
                .IsRequired()
                .HasMaxLength(50)
                .HasDefaultValue(RobotStatus.Registered);

            entity.Property(robot => robot.DeviceTokenHash)
                .HasMaxLength(255);

            entity.Property(robot => robot.IpAddress)
                .HasMaxLength(45);

            entity.Property(robot => robot.CreatedAt)
                .IsRequired();

            entity.HasIndex(robot => robot.UserId)
                .HasDatabaseName("IX_robots_user_id");

            entity.HasIndex(robot => robot.CompanyId)
                .HasDatabaseName("IX_robots_company_id");

            entity.HasIndex(robot => robot.Status)
                .HasDatabaseName("IX_robots_status");

            entity.HasIndex(robot => new { robot.UserId, robot.Status })
                .HasDatabaseName("IX_robots_user_status");

            entity.HasIndex(robot => new { robot.CompanyId, robot.Status })
                .HasDatabaseName("IX_robots_company_status");

            entity.HasOne(robot => robot.User)
                .WithMany()
                .HasForeignKey(robot => robot.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(robot => robot.Company)
                .WithMany(company => company.Robots)
                .HasForeignKey(robot => robot.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);

        });
    }

    private static void ConfigureRobotSafetyPolicies(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RobotSafetyPolicy>(entity =>
        {
            entity.ToTable("robot_safety_policies");

            entity.HasKey(policy => policy.Id);

            entity.Property(policy => policy.Scope)
                .HasConversion<string>()
                .IsRequired()
                .HasMaxLength(30);

            entity.Property(policy => policy.Name)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(policy => policy.RobotModel)
                .IsRequired()
                .HasMaxLength(80);

            entity.Property(policy => policy.PolicyJson)
                .IsRequired()
                .HasColumnType("nvarchar(max)");

            entity.Property(policy => policy.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            entity.Property(policy => policy.CreatedAt)
                .IsRequired();

            entity.HasIndex(policy => policy.CompanyId)
                .HasDatabaseName("IX_robot_safety_policies_company_id");

            entity.HasIndex(policy => policy.RobotId)
                .HasDatabaseName("IX_robot_safety_policies_robot_id");

            entity.HasIndex(policy => new { policy.CompanyId, policy.Scope, policy.IsActive })
                .HasDatabaseName("IX_robot_safety_policies_company_scope_active");

            entity.HasIndex(policy => new { policy.RobotId, policy.Scope, policy.IsActive })
                .HasDatabaseName("IX_robot_safety_policies_robot_scope_active");

            entity.HasIndex(policy => new { policy.CompanyId, policy.Scope })
                .IsUnique()
                .HasFilter("[RobotId] IS NULL AND [IsActive] = 1")
                .HasDatabaseName("UX_robot_safety_policies_company_active");

            entity.HasIndex(policy => new { policy.RobotId, policy.Scope })
                .IsUnique()
                .HasFilter("[RobotId] IS NOT NULL AND [IsActive] = 1")
                .HasDatabaseName("UX_robot_safety_policies_robot_active");

            entity.HasOne(policy => policy.Company)
                .WithMany(company => company.SafetyPolicies)
                .HasForeignKey(policy => policy.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(policy => policy.Robot)
                .WithMany(robot => robot.SafetyPolicies)
                .HasForeignKey(policy => policy.RobotId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(policy => policy.CreatedByUser)
                .WithMany()
                .HasForeignKey(policy => policy.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(policy => policy.UpdatedByUser)
                .WithMany()
                .HasForeignKey(policy => policy.UpdatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureRobotRuntimeSessions(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RobotRuntimeSession>(entity =>
        {
            entity.ToTable("robot_runtime_sessions");

            entity.HasKey(session => session.Id);

            entity.Property(session => session.StartedAt)
                .IsRequired();

            entity.Property(session => session.LastSeenAt)
                .IsRequired();

            entity.Property(session => session.EndReason)
                .HasMaxLength(50);

            entity.Property(session => session.CreatedAt)
                .IsRequired();

            entity.HasIndex(session => new { session.RobotId, session.StartedAt })
                .HasDatabaseName("IX_robot_runtime_sessions_robot_started_at");

            entity.HasIndex(session => new { session.RobotId, session.EndedAt })
                .HasDatabaseName("IX_robot_runtime_sessions_robot_ended_at");

            entity.HasIndex(session => new { session.RobotId, session.EndedAt, session.EndReason })
                .HasDatabaseName("IX_robot_runtime_sessions_robot_ended_at_reason");

            entity.HasIndex(session => session.RobotId)
                .IsUnique()
                .HasFilter("[EndedAt] IS NULL")
                .HasDatabaseName("UX_robot_runtime_sessions_robot_open");

            entity.HasOne(session => session.Robot)
                .WithMany(robot => robot.RuntimeSessions)
                .HasForeignKey(session => session.RobotId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureRobotCommands(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RobotCommand>(entity =>
        {
            entity.ToTable("robot_commands");

            entity.HasKey(command => command.Id);

            entity.Property(command => command.CommandType)
                .HasConversion<string>()
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(command => command.PayloadJson)
                .HasColumnType("nvarchar(max)");

            entity.Property(command => command.Status)
                .HasConversion<string>()
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(command => command.CreatedAt)
                .IsRequired();

            entity.Property(command => command.TimeoutAt);

            entity.Property(command => command.FailureReason)
                .HasMaxLength(500);

            entity.HasIndex(command => command.RobotId)
                .HasDatabaseName("IX_robot_commands_robot_id");

            entity.HasIndex(command => command.UserId)
                .HasDatabaseName("IX_robot_commands_user_id");

            entity.HasIndex(command => new { command.RobotId, command.Status, command.CreatedAt })
                .HasDatabaseName("IX_robot_commands_robot_status_created_at");

            entity.HasIndex(command => new { command.Status, command.TimeoutAt })
                .HasDatabaseName("IX_robot_commands_status_timeout_at");

            entity.HasOne(command => command.Robot)
                .WithMany(robot => robot.Commands)
                .HasForeignKey(command => command.RobotId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(command => command.User)
                .WithMany()
                .HasForeignKey(command => command.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(command => command.DeliveryAttemptCount)
    .IsRequired()
    .HasDefaultValue(0);
        });
    }

    private static void ConfigureCommandResults(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CommandResult>(entity =>
        {
            entity.ToTable("command_results");

            entity.HasKey(result => result.Id);

            entity.Property(result => result.Message)
                .HasMaxLength(500);

            entity.Property(result => result.RawPayloadJson)
                .HasColumnType("nvarchar(max)");

            entity.Property(result => result.CompletedAt)
                .IsRequired();

            entity.HasIndex(result => result.CommandId)
                .IsUnique()
                .HasDatabaseName("UX_command_results_command_id");

            entity.HasIndex(result => result.RobotId)
                .HasDatabaseName("IX_command_results_robot_id");

            entity.HasOne(result => result.Command)
                .WithOne(command => command.Result)
                .HasForeignKey<CommandResult>(result => result.CommandId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(result => result.Robot)
                .WithMany(robot => robot.CommandResults)
                .HasForeignKey(result => result.RobotId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureAuditLogs(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("audit_logs");

            entity.HasKey(log => log.Id);

            entity.Property(log => log.Action)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(log => log.IpAddress)
                .HasMaxLength(45);

            entity.Property(log => log.Message)
                .HasMaxLength(500);

            entity.Property(log => log.RawPayloadJson)
                .HasColumnType("nvarchar(max)");

            entity.Property(log => log.CreatedAt)
                .IsRequired();

            entity.HasIndex(log => log.UserId)
                .HasDatabaseName("IX_audit_logs_user_id");

            entity.HasIndex(log => log.RobotId)
                .HasDatabaseName("IX_audit_logs_robot_id");

            entity.HasIndex(log => log.CreatedAt)
                .HasDatabaseName("IX_audit_logs_created_at");

            entity.HasIndex(log => new { log.RobotId, log.CreatedAt })
                .HasDatabaseName("IX_audit_logs_robot_created_at");

            entity.HasOne(log => log.User)
                .WithMany()
                .HasForeignKey(log => log.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(log => log.Robot)
                .WithMany(robot => robot.AuditLogs)
                .HasForeignKey(log => log.RobotId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureRobotPrograms(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RobotProgram>(entity =>
        {
            entity.ToTable("robot_programs");

            entity.HasKey(program => program.Id);

            entity.Property(program => program.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(program => program.Status)
                .HasConversion<string>()
                .IsRequired()
                .HasMaxLength(50)
                .HasDefaultValue(RobotProgramStatus.Draft);

            entity.Property(program => program.Source)
                .HasConversion<string>()
                .IsRequired()
                .HasMaxLength(50)
                .HasDefaultValue(RobotProgramSource.Studio);

            entity.Property(program => program.CreatedAt)
                .IsRequired();

            entity.HasIndex(program => program.RobotId)
                .HasDatabaseName("IX_robot_programs_robot_id");

            entity.HasIndex(program => program.CreatedByUserId)
                .HasDatabaseName("IX_robot_programs_created_by_user_id");

            entity.HasIndex(program => new { program.RobotId, program.Status })
                .HasDatabaseName("IX_robot_programs_robot_status");

            entity.HasOne(program => program.Robot)
                .WithMany(robot => robot.Programs)
                .HasForeignKey(program => program.RobotId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(program => program.CreatedByUser)
                .WithMany()
                .HasForeignKey(program => program.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureRobotProgramSteps(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RobotProgramStep>(entity =>
        {
            entity.ToTable("robot_program_steps");

            entity.HasKey(step => step.Id);

            entity.Property(step => step.OrderIndex)
                .IsRequired();

            entity.Property(step => step.StepType)
                .HasConversion<string>()
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(step => step.Label)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(step => step.PayloadJson)
                .IsRequired()
                .HasColumnType("nvarchar(max)");

            entity.Property(step => step.CreatedAt)
                .IsRequired();

            entity.HasIndex(step => step.ProgramId)
                .HasDatabaseName("IX_robot_program_steps_program_id");

            entity.HasIndex(step => new { step.ProgramId, step.OrderIndex })
                .IsUnique()
                .HasDatabaseName("UX_robot_program_steps_program_order");

            entity.HasOne(step => step.Program)
                .WithMany(program => program.Steps)
                .HasForeignKey(step => step.ProgramId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
