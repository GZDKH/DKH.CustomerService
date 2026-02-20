namespace DKH.CustomerService.Application.ExternalIdentities.MergeProfiles;

public class MergeProfilesCommandValidator : AbstractValidator<MergeProfilesCommand>
{
    public MergeProfilesCommandValidator()
    {
        RuleFor(x => x.StorefrontId).NotEmpty();
        RuleFor(x => x.SourceUserId).NotEmpty();
        RuleFor(x => x.TargetUserId).NotEmpty();
        RuleFor(x => x)
            .Must(x => x.SourceUserId != x.TargetUserId)
            .WithMessage("Source and target user IDs must be different.");
    }
}
