using DKH.CustomerService.Domain.Enums;

namespace DKH.CustomerService.Domain.ValueObjects;

public sealed class AccountStatus
{
    public AccountStatusType Status { get; private set; } = AccountStatusType.Active;

    public DateTime? BlockedAt { get; private set; }

    public string? BlockReason { get; private set; }

    public string? BlockedBy { get; private set; }

    public DateTime? SuspendedUntil { get; private set; }

    public DateTime? LastLoginAt { get; private set; }

    public DateTime? LastActivityAt { get; private set; }

    public int TotalOrdersCount { get; private set; }

    public decimal TotalSpent { get; private set; }

    public static AccountStatus CreateNew() => new();

    public void Block(string reason, string blockedBy)
    {
        Status = AccountStatusType.Blocked;
        BlockedAt = DateTime.UtcNow;
        BlockReason = reason;
        BlockedBy = blockedBy;
        SuspendedUntil = null;
    }

    public void Unblock()
    {
        Status = AccountStatusType.Active;
        BlockedAt = null;
        BlockReason = null;
        BlockedBy = null;
    }

    public void Suspend(DateTime suspendUntil, string reason, string suspendedBy)
    {
        Status = AccountStatusType.Suspended;
        SuspendedUntil = suspendUntil;
        BlockReason = reason;
        BlockedBy = suspendedBy;
        BlockedAt = DateTime.UtcNow;
    }

    public void MarkDeleted()
    {
        Status = AccountStatusType.Deleted;
    }

    public void RecordLogin()
    {
        LastLoginAt = DateTime.UtcNow;
        LastActivityAt = DateTime.UtcNow;
    }

    public void RecordActivity()
    {
        LastActivityAt = DateTime.UtcNow;
    }

    public void UpdateOrderStats(int ordersCount, decimal totalSpent)
    {
        TotalOrdersCount = ordersCount;
        TotalSpent = totalSpent;
    }

    public bool IsActive => Status == AccountStatusType.Active;

    public bool IsBlocked => Status == AccountStatusType.Blocked;

    public bool IsSuspended => Status == AccountStatusType.Suspended &&
                               SuspendedUntil.HasValue &&
                               SuspendedUntil.Value > DateTime.UtcNow;
}
