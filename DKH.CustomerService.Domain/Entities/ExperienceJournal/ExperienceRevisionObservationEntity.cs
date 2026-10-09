using DKH.CustomerService.Domain.Entities.ProductCollection;
using DKH.CustomerService.Domain.ValueObjects;
using DKH.Platform.Domain.Entities.Auditing;

namespace DKH.CustomerService.Domain.Entities.ExperienceJournal;

public sealed class ExperienceRevisionObservationEntity : FullAuditedEntityWithKey<Guid>
{
    private ExperienceRevisionObservationEntity() { }

    private ExperienceRevisionObservationEntity(Guid accountId, Guid revisionId, ExperienceObservationValue value)
        : base(Guid.NewGuid())
    {
        AccountId = accountId;
        RevisionId = revisionId;
        DefinitionId = value.DefinitionId;
        Role = value.Role;
        ValueType = value.ValueType;
        TextValue = value.TextValue;
        DecimalValue = value.DecimalValue;
        IntegerValue = value.IntegerValue;
        BooleanValue = value.BooleanValue;
        UnitCode = value.UnitCode;
        RowId = value.RowId;
        RowOrdinal = value.RowOrdinal;
        OptionId = value.OptionId;
    }

    public Guid AccountId { get; private set; }
    public Guid RevisionId { get; private set; }
    public Guid DefinitionId { get; private set; }
    public ProductExperienceObservationRole Role { get; private set; }
    public ProductExperienceObservationValueType ValueType { get; private set; }
    public string? TextValue { get; private set; }
    public decimal? DecimalValue { get; private set; }
    public long? IntegerValue { get; private set; }
    public bool? BooleanValue { get; private set; }
    public string? UnitCode { get; private set; }
    public Guid? RowId { get; private set; }
    public int? RowOrdinal { get; private set; }
    public Guid? OptionId { get; private set; }

    public override object?[] GetKeys() => [Id];

    public static ExperienceRevisionObservationEntity Create(Guid accountId, Guid revisionId, ExperienceObservationValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (accountId == Guid.Empty || revisionId == Guid.Empty)
        {
            throw new ArgumentException("An owner and immutable revision are required.");
        }

        return new ExperienceRevisionObservationEntity(accountId, revisionId, value);
    }

    public ExperienceObservationValue ReadValue()
        => ExperienceObservationValue.Create(DefinitionId, Role, ValueType, TextValue, DecimalValue, IntegerValue,
            BooleanValue, UnitCode, RowId, RowOrdinal, OptionId);
}
