using FluentValidation;
using Kamada.Shared.Contracts.Common;
using Kamada.Shared.Contracts.Workforce;

namespace Kamada.Modules.Workforce.Application.Workers;

internal sealed class CreateWorkerRequestValidator : AbstractValidator<CreateWorkerRequest>
{
    public CreateWorkerRequestValidator()
    {
        RuleFor(r => r.Username).ValidUsername();
        RuleFor(r => r.Password).StrongPassword();
        RuleFor(r => r.Name).PersonName();
        RuleFor(r => r.Surname).PersonName();
        RuleFor(r => r.PhoneNumber).PhoneNumber();
        RuleFor(r => r.Roles).WorkerRoles();
    }
}

internal sealed class UpdateWorkerRequestValidator : AbstractValidator<UpdateWorkerRequest>
{
    public UpdateWorkerRequestValidator()
    {
        RuleFor(r => r.Name).PersonName();
        RuleFor(r => r.Surname).PersonName();
        RuleFor(r => r.PhoneNumber).PhoneNumber();
        RuleFor(r => r.Roles).WorkerRoles();
    }
}

internal sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator() => RuleFor(r => r.NewPassword).StrongPassword();
}

internal sealed class WorkerListQueryValidator : AbstractValidator<WorkerListQuery>
{
    public WorkerListQueryValidator()
    {
        RuleFor(q => q.Search).MaximumLength(100);
        RuleFor(q => q.Role).IsInEnum();
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        RuleFor(q => q.PageSize).InclusiveBetween(1, Paging.MaxPageSize);
    }
}
