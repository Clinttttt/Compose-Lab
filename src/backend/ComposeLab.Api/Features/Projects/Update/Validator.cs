using ComposeLab.Api.Infrastructure.Validation;
using FluentValidation;

namespace ComposeLab.Api.Features.Projects.Update;

internal sealed class Validator : AbstractValidator<Command>
{
    public Validator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(120);

        // The envelope only, exactly as on create. A broken architecture stays saveable.
        RuleFor(command => command.Topology).SetValidator(new TopologyDocumentEnvelopeValidator());
    }
}
