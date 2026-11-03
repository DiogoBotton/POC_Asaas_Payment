using API_Payment_Asaas.Models;
using Microsoft.EntityFrameworkCore;

namespace API_Payment_Asaas.Data;

public sealed class PaymentDbContext(DbContextOptions<PaymentDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<AsaasStoredCard> AsaasStoredCards => Set<AsaasStoredCard>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<WebhookEvent> WebhookEvents => Set<WebhookEvent>();
    public DbSet<FiscalConfiguration> FiscalConfigurations => Set<FiscalConfiguration>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasIndex(customer => customer.AsaasCustomerId).IsUnique();
            entity.Property(customer => customer.Name).HasMaxLength(200);
            entity.Property(customer => customer.Email).HasMaxLength(320);
            entity.Property(customer => customer.CpfCnpj).HasMaxLength(20);
            entity.Property(customer => customer.Phone).HasMaxLength(30);
            entity.Property(customer => customer.PostalCode).HasMaxLength(9);
            entity.Property(customer => customer.Address).HasMaxLength(255);
            entity.Property(customer => customer.AddressNumber).HasMaxLength(20);
            entity.Property(customer => customer.AddressComplement).HasMaxLength(255);
            entity.Property(customer => customer.Province).HasMaxLength(100);
        });

        modelBuilder.Entity<AsaasStoredCard>(entity =>
        {
            entity.HasIndex(card => card.TokenFingerprint).IsUnique();
            entity.Property(card => card.ProtectedToken).HasMaxLength(2048);
            entity.Property(card => card.TokenFingerprint).HasMaxLength(64);
            entity.Property(card => card.Brand).HasMaxLength(30);
            entity.Property(card => card.LastFourDigits).HasMaxLength(4);
            entity.HasOne(card => card.Customer)
                .WithMany(customer => customer.StoredCards)
                .HasForeignKey(card => card.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Subscription>(entity =>
        {
            entity.HasIndex(subscription => subscription.AsaasSubscriptionId).IsUnique();
            entity.Property(subscription => subscription.Value).HasPrecision(18, 2);
            entity.Property(subscription => subscription.Cycle).HasConversion<string>();
            entity.Property(subscription => subscription.PaymentMethod).HasConversion<string>();
            entity.Property(subscription => subscription.Description).HasMaxLength(500);
            entity.Property(subscription => subscription.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(subscription => subscription.ErrorMessage).HasMaxLength(2000);
            entity.HasOne(subscription => subscription.Customer)
                .WithMany(customer => customer.Subscriptions)
                .HasForeignKey(subscription => subscription.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasIndex(payment => payment.AsaasPaymentId).IsUnique();
            entity.Property(payment => payment.Value).HasPrecision(18, 2);
            entity.Property(payment => payment.PaymentMethod).HasConversion<string>();
            entity.Property(payment => payment.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(payment => payment.ErrorMessage).HasMaxLength(2000);
            entity.Property(payment => payment.BankSlipIdentificationField).HasMaxLength(100);
            entity.HasOne(payment => payment.Customer)
                .WithMany(customer => customer.Payments)
                .HasForeignKey(payment => payment.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(payment => payment.Subscription)
                .WithMany(subscription => subscription.Payments)
                .HasForeignKey(payment => payment.SubscriptionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasIndex(invoice => invoice.AsaasInvoiceId).IsUnique();
            entity.HasIndex(invoice => invoice.PaymentId).IsUnique();
            entity.Property(invoice => invoice.WorkflowStatus).HasConversion<string>().HasMaxLength(40);
            entity.Property(invoice => invoice.AsaasStatus).HasColumnName("Status").HasMaxLength(100);
            entity.Property(invoice => invoice.FiscalMunicipalServiceId).HasMaxLength(50);
            entity.Property(invoice => invoice.FiscalMunicipalServiceName).HasMaxLength(255);
            entity.Property(invoice => invoice.FiscalServiceDescription).HasMaxLength(500);
            entity.Property(invoice => invoice.FiscalObservations).HasMaxLength(2000);
            entity.Property(invoice => invoice.FiscalIss).HasPrecision(9, 4);
            entity.Property(invoice => invoice.FiscalPis).HasPrecision(9, 4);
            entity.Property(invoice => invoice.FiscalCofins).HasPrecision(9, 4);
            entity.Property(invoice => invoice.FiscalCsll).HasPrecision(9, 4);
            entity.Property(invoice => invoice.FiscalInss).HasPrecision(9, 4);
            entity.Property(invoice => invoice.FiscalIr).HasPrecision(9, 4);
            entity.HasOne(invoice => invoice.Payment)
                .WithOne(payment => payment.Invoice)
                .HasForeignKey<Invoice>(invoice => invoice.PaymentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WebhookEvent>(entity =>
        {
            entity.HasIndex(webhookEvent => webhookEvent.AsaasEventId).IsUnique();
            entity.Property(webhookEvent => webhookEvent.EventType).HasMaxLength(100);
            entity.Property(webhookEvent => webhookEvent.Payload).HasColumnType("jsonb");
        });

        modelBuilder.Entity<FiscalConfiguration>(entity =>
        {
            entity.Property(configuration => configuration.MunicipalServiceId).HasMaxLength(50);
            entity.Property(configuration => configuration.MunicipalServiceName).HasMaxLength(255);
            entity.Property(configuration => configuration.ServiceDescription).HasMaxLength(500);
            entity.Property(configuration => configuration.Observations).HasMaxLength(2000);
            entity.Property(configuration => configuration.Iss).HasPrecision(9, 4);
            entity.Property(configuration => configuration.Pis).HasPrecision(9, 4);
            entity.Property(configuration => configuration.Cofins).HasPrecision(9, 4);
            entity.Property(configuration => configuration.Csll).HasPrecision(9, 4);
            entity.Property(configuration => configuration.Inss).HasPrecision(9, 4);
            entity.Property(configuration => configuration.Ir).HasPrecision(9, 4);
        });
    }
}