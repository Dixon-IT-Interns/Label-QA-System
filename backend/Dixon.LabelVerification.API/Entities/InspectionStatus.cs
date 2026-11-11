namespace Dixon.LabelVerification.API.Entities;

public enum InspectionStatus
{
    READY,
    PASS,
    FAIL,
    REVIEW_REQUIRED,
    PROCESSING,
    BLOCKED,
    ERROR,
    CANCELLED,
    NOT_APPLICABLE
}

public enum LabelType
{
    CO,
    CONFIG
}

public enum ImageRole
{
    MASTER,
    ACTUAL
}

public enum InspectionImageType
{
    MASTER_CO,
    ACTUAL_CO,
    MASTER_CONFIG,
    ACTUAL_CONFIG
}

public enum InspectionStageType
{
    CO_COMPARISON,
    CONFIG_COMPARISON,
    SERIAL_VALIDATION
}
