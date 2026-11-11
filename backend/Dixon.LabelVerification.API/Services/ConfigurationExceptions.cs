namespace Dixon.LabelVerification.API.Services;

public sealed class ConfigurationNotFoundException(string message) : Exception(message);

public sealed class ConfigurationConflictException(string message) : Exception(message);
