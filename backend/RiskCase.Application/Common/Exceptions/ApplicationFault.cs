namespace RiskCase.Application.Common.Exceptions;

public sealed class ApplicationFault(int statusCode, string code, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
}
