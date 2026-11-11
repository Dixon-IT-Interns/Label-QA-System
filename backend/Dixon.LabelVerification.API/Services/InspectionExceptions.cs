namespace Dixon.LabelVerification.API.Services;

public sealed class InspectionNotFoundException(string message) : Exception(message);

public sealed class InspectionConflictException(string message) : Exception(message);

public sealed class InspectionValidationException(string message) : Exception(message);

public sealed class InspectionProcessingException(string message, Exception? inner = null)
    : Exception(message, inner);
