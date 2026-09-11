using SmartDrag.Core.Payload;

namespace SmartDrag.Windows.Probe;

internal sealed record ProbePayloadExpectation(
    ProbeMode Mode,
    PayloadQualificationResult? PreflightQualification);
