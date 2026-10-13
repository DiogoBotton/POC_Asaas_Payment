using API_Payment_Asaas.Models;
using Microsoft.EntityFrameworkCore;

namespace API_Payment_Asaas.Data;

public sealed class PaymentDbContext(DbContextOptions<PaymentDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<WebhookEvent> WebhookEvents => Set<WebhookEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasIndex(customer => customer.AsaasCustomerId).IsUnique();
            entity.Property(customer => customer.Name).HasMaxLength(200);
            entity.Property(customer => customer.Email).HasMaxLength(320);
            entity.Property(customer => customer.CpfCnpj).HasMaxLength(20);
            entity.Property(customer => customer.Phone).HasMaxLength(30);
        });

        modelBuilder.Entity<Subscription>(entity =>
        {
            entity.HasIndex(subscription => subscription.AsaasSubscriptionId).IsUnique();
            entity.Property(subscription => subscription.Value).HasPrecision(18, 2);
            entity.Property(subscription => subscription.Cycle).HasConversion<string>();
            entity.Property(subscription => subscription.PaymentMethod).HasConversion<string>();
            entity.Property(subscription => subscription.Status).HasMaxLength(40);
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
            entity.Property(payment => payment.Status).HasMaxLength(40);
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
            entity.Property(invoice => invoice.Status).HasMaxLength(40);
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
    }
}