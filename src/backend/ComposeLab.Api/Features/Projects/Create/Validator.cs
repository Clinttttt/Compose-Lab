using ComposeLab.Api.Infrastructure.Validation;
using FluentValidation;

namespace ComposeLab.Api.Features.Projects.Create;

internal sealed class Validator : AbstractValidator<Command>
{
    public Validator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(120);

        // Deliberately only the envelope. TopologyValidator is never run as a save gate: a structurally
        // broken architecture is something ComposeLab exists to let learners build and keep.
        RuleFor(command => command.Topology).SetValidator(new TopologyDocumentEnvelopeValidator());
    }
}
