using eInvWorld.Models;
using eInvWorld.Models.Audit;
using eInvWorld.Models.Auth;
using eInvWorld.Models.InputModel;
using eInvWorld.Models.JsonModels;
using eInvWorld.Models.Logs;
using eInvWorld.Models.Settings;
using eInvWorld.Models.Templates;
using eInvWorld.Models.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace eInvWorld.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<LHDNToken> LHDNTokens { get; set; }

        public DbSet<InvoiceHistory> InvoiceHistories { get; set; }
        public DbSet<ContactUs> ContactUs { get; set; }
        public DbSet<InvoiceSubmission> InvoiceSubmissions { get; set; }
        public DbSet<Status> Statuses { get; set; }
        public DbSet<RegistrationType> RegistrationTypes { get; set; }
        public DbSet<UserCompany> UserCompanies { get; set; }

        public DbSet<ActivityLog> ActivityLogs { get; set; }
        public DbSet<PartyInfo> PartyInfos { get; set; } = default!;
        public DbSet<PublicCustomer> PublicCustomers { get; set; }

        public DbSet<SupplierBuyer> SupplierBuyers { get; set; }
        public DbSet<TaxSummary> TaxSummaries { get; set; }  
        public DbSet<GlobalThemeSettings> GlobalThemeSettings { get; set; } = default!;
        public DbSet<InvoiceHeader> InvoiceHeaders { get; set; } = default!;
        public DbSet<Models.InputModel.InvoiceLine> InvoiceLines { get; set; } = default!;
        public DbSet<InvoiceTax> InvoiceTaxes { get; set; } = default!;
        public DbSet<InvoiceTest> InvoiceTests { get; set; } = default!;
        public DbSet<InvoiceForm> InvoiceForms { get; set; } = default!;
        public DbSet<EmailNotification> Notifications { get; set; } = default!;
        public DbSet<Supplier> Suppliers { get; set; } = default!;
        public DbSet<Buyer> Buyers { get; set; } = default!;
        public DbSet<EInvoiceType> EInvoiceTypes { get; set; } = default!;
        public DbSet<ClassificationCode> ClassificationCodes { get; set; } = default!;
        public DbSet<CountryCode> CountryCodes { get; set; } = default!;
        public DbSet<CurrencyCode> CurrencyCodes { get; set; } = default!;
        public DbSet<MSICSubCategoryCode> MSICSubCategoryCodes { get; set; } = default!;
        public DbSet<PaymentMode> PaymentMethods { get; set; } = default!;
        public DbSet<StateCode> StateCodes { get; set; } = default!;
        public DbSet<TaxType> TaxTypes { get; set; } = default!;
        public DbSet<UnitType> UnitTypes { get; set; } = default!;
        public DbSet<ItemDescription> ItemDescriptions { get; set; } = default!;
        public IEnumerable<object>? DocumentTypes { get; internal set; }

        public DbSet<InvoiceTemplate> InvoiceTemplates { get; set; }
        public DbSet<InvoiceTemplateLine> InvoiceTemplateLines { get; set; }
        public DbSet<InvoiceTemplateTax> InvoiceTemplateTaxes { get; set; }

        // --- Recurring Invoices ---
        public DbSet<eInvWorld.Models.Recurring.RecurringProfile> RecurringProfiles { get; set; }
        public DbSet<eInvWorld.Models.Recurring.RecurringRunHistory> RecurringRunHistories { get; set; }

        // --- Background sync/import job tracking ---
        public DbSet<eInvWorld.Models.Background.SyncJob> SyncJobs { get; set; }

        //for dashboard
        public DbSet<InvoiceTopProduct> InvoiceTopProducts { get; set; }
        public DbSet<InvoiceKpiSummary> InvoiceKpiSummaries { get; set; }
        public DbSet<InvoiceByCustomerSummary> InvoiceByCustomerSummaries { get; set; }
        public DbSet<InvoiceRejectedReason> InvoiceRejectedReasons { get; set; }
        public DbSet<InvoiceTypeBreakdown> InvoiceTypeBreakdowns { get; set; }
        public DbSet<InvoiceMonthlySummary> InvoiceMonthlySummaries { get; set; }
        public DbSet<InvoiceTaxSummary> InvoiceTaxSummaries { get; set; }

        //logs
        public DbSet<LHDNTokenLog> LHDNTokenLogs { get; set; }
        public DbSet<UserActivityLog> UserActivityLogs { get; set; }

        // NOTE: SystemLogs is intentionally NOT an EF DbSet/entity. The table is owned and auto-created by
        // the Serilog MSSqlServer sink (autoCreateSqlTable=true). The admin Logs page reads it via
        // Database.SqlQueryRaw<SystemLog>. See Models/SystemLog.cs.
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Keep ASP.NET Identity key columns at their original length (128) to match the EXISTING
            // database. The newer Identity/EF default would widen them to nvarchar(450); applying that
            // would push the composite clustered primary keys past SQL Server's 900-byte index-key limit
            // and fail. Provider names, keys and token names are short, so 128 is more than enough.
            modelBuilder.Entity<IdentityUserLogin<string>>(b =>
            {
                b.Property(l => l.LoginProvider).HasMaxLength(128);
                b.Property(l => l.ProviderKey).HasMaxLength(128);
            });
            modelBuilder.Entity<IdentityUserToken<string>>(b =>
            {
                b.Property(t => t.LoginProvider).HasMaxLength(128);
                b.Property(t => t.Name).HasMaxLength(128);
            });

            // Default money precision: 18,2 for all decimal columns…
            foreach (var property in modelBuilder.Model.GetEntityTypes()
                        .SelectMany(t => t.GetProperties())
                        .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
            {
                property.SetColumnType("decimal(18, 2)");
            }

            // …but rate/quantity/unit-price fields need more decimal places than money totals.
            // Truncating these to 2dp corrupts foreign-currency invoices and fractional quantities.
            modelBuilder.Entity<InvoiceHeader>()
                .Property(h => h.ExchangeRate).HasColumnType("decimal(18, 6)");
            modelBuilder.Entity<Models.InputModel.InvoiceLine>()
                .Property(l => l.Quantity).HasColumnType("decimal(18, 6)");
            modelBuilder.Entity<Models.InputModel.InvoiceLine>()
                .Property(l => l.UnitPrice).HasColumnType("decimal(18, 4)");

            // Existing Configurations
            modelBuilder.Entity<LHDNToken>().HasKey(t => t.Id);
            modelBuilder.Entity<LHDNToken>().HasIndex(t => t.TIN).IsUnique();

            modelBuilder.Entity<LHDNTokenLog>().ToTable("LHDNTokenLogs");
            modelBuilder.Entity<LHDNTokenLog>().HasKey(log => log.Id);
            modelBuilder.Entity<LHDNTokenLog>().Property(log => log.TIN).IsRequired().HasMaxLength(20);
            modelBuilder.Entity<LHDNTokenLog>().Property(log => log.ClientIdUsed).IsRequired().HasMaxLength(100);
            modelBuilder.Entity<LHDNTokenLog>().Property(log => log.Source).HasMaxLength(50);

            modelBuilder.Entity<UserActivityLog>().ToTable("UserActivityLogs");
            modelBuilder.Entity<UserActivityLog>().HasKey(l => l.Id);
            modelBuilder.Entity<UserActivityLog>().Property(l => l.Action).IsRequired().HasMaxLength(100);
            modelBuilder.Entity<UserActivityLog>().Property(l => l.UserId).IsRequired();
            modelBuilder.Entity<UserActivityLog>().Property(l => l.UserName).IsRequired();

            modelBuilder.Entity<InvoiceTopProduct>().HasNoKey();
            modelBuilder.Entity<InvoiceKpiSummary>().HasNoKey();
            modelBuilder.Entity<InvoiceByCustomerSummary>().HasNoKey();
            modelBuilder.Entity<InvoiceRejectedReason>().HasNoKey();
            modelBuilder.Entity<InvoiceTypeBreakdown>().HasNoKey();
            modelBuilder.Entity<InvoiceMonthlySummary>().HasNoKey();
            modelBuilder.Entity<InvoiceTaxSummary>().HasNoKey();

            modelBuilder.Entity<UserCompany>()
                .HasOne(uc => uc.User)
                .WithMany(u => u.UserCompanies)
                .HasForeignKey(uc => uc.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserCompany>()
                .HasOne(uc => uc.PartyInfo)
                .WithMany(p => p.UserCompanies)
                .HasForeignKey(uc => uc.PartyInfoId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Status>().HasData(
               new Status { StatusCode = "Draft", StatusType = "Internal", Name = "Draft", Description = "Invoice is in draft state" },
               new Status { StatusCode = "Submitted", StatusType = "LHDN", Name = "Submitted", Description = "Invoice has been submitted to LHDN" },
               new Status { StatusCode = "Valid", StatusType = "LHDN", Name = "Valid", Description = "Invoice has been validated successfully by LHDN" },
               new Status { StatusCode = "Invalid", StatusType = "LHDN", Name = "Invalid", Description = "Invoice was rejected by LHDN" },
               new Status { StatusCode = "Cancelled", StatusType = "LHDN", Name = "Cancelled", Description = "Invoice has been cancelled" },
               new Status { StatusCode = "RequestReject", StatusType = "Internal", Name = "Request Reject", Description = "Invoice is flagged for resubmission" },
               new Status { StatusCode = "Completed", StatusType = "Internal", Name = "Completed", Description = "Invoice process is completed" }
            );

            modelBuilder.Entity<RegistrationType>().HasData(
                new RegistrationType { Code = "NRIC", Name = "Identification Card No." },
                new RegistrationType { Code = "PASSPORT", Name = "Passport No." },
                new RegistrationType { Code = "BRN", Name = "Business Registration No." },
                new RegistrationType { Code = "ARMY", Name = "Army No." }
            );

            modelBuilder.Entity<InvoiceSubmission>()
                .HasOne(s => s.InternalStatus)
                .WithMany()
                .HasForeignKey(s => s.InternalStatusId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<InvoiceSubmission>()
                .HasOne(s => s.LHDNStatus)
                .WithMany()
                .HasForeignKey(s => s.LHDNStatusId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<InvoiceHeader>()
                .HasOne(i => i.Supplier)
                .WithMany()
                .HasForeignKey(i => i.SupplierId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<InvoiceHeader>()
                .HasOne(i => i.Customer)
                .WithMany()
                .HasForeignKey(i => i.CustomerId)
                .OnDelete(DeleteBehavior.NoAction);

            // Indexes for hot lookup/filter columns (status sync, search, detail pages).
            // RefDocumentNo and InvoiceDirection are nvarchar(max) today, which SQL Server cannot
            // index, so they are given a bounded length first (generous vs the actual value formats).
            // UUID is already MaxLength(100) and InvoiceHistory.InvoiceNo already MaxLength(50).
            modelBuilder.Entity<InvoiceHeader>(b =>
            {
                b.Property(i => i.RefDocumentNo).HasMaxLength(200);
                b.Property(i => i.InvoiceDirection).HasMaxLength(50);
                b.HasIndex(i => i.UUID);
                b.HasIndex(i => i.RefDocumentNo);
                b.HasIndex(i => i.InvoiceDirection);
                b.HasIndex(i => i.CreatedDate);
            });

            modelBuilder.Entity<InvoiceHistory>(b =>
            {
                b.HasIndex(h => h.InvoiceNo);
            });

            modelBuilder.Entity<PartyInfo>()
                    .HasIndex(p => p.TIN)
                    .IsUnique()
                    .HasFilter("[TIN] <> 'EI00000000010' AND [TIN] <> 'EI00000000020' AND [TIN] <> 'EI00000000030' AND [TIN] <> 'EI00000000040'");

            modelBuilder.Entity<SupplierBuyer>()
                            .HasKey(sb => sb.Id);

            // Configure Supplier Relationship
            modelBuilder.Entity<SupplierBuyer>()
                .HasOne(sb => sb.Supplier)
                .WithMany(s => s.AssignedBuyers)
                .HasForeignKey(sb => sb.SupplierId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<SupplierBuyer>()
                .HasOne(sb => sb.Buyer)
                .WithMany(b => b.AssignedSuppliers)
                .HasForeignKey(sb => sb.BuyerId)
                .IsRequired(false) // <--- Allows BuyerId to be null
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<InvoiceTemplate>()
                .HasMany(t => t.InvoiceLines)
                .WithOne()
                .HasForeignKey(l => l.InvoiceTemplateId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<InvoiceTemplateLine>()
                .HasMany(l => l.Taxes)
                .WithOne()
                .HasForeignKey(t => t.InvoiceTemplateLineId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
