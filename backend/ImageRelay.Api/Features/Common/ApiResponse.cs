namespace ImageRelay.Api.Features.Common;

public static class ApiResponse
{
    public static IResult Ok() => Results.Ok(new ApiSuccessResponse());

    public static IResult Ok<T>(T data) => Results.Ok(new ApiSuccessResponse<T>(data));

    public static IResult BadRequest(string error) => Fail(StatusCodes.Status400BadRequest, error);

    public static IResult Unauthorized(string error = "unauthorized") => Fail(StatusCodes.Status401Unauthorized, error);

    public static IResult NotFound(string error = "not found") => Fail(StatusCodes.Status404NotFound, error);

    public static IResult Conflict(string error) => Fail(StatusCodes.Status409Conflict, error);

    public static IResult Fail(int statusCode, string error) => Results.Json(
        new ApiFailureResponse(error),
        statusCode: statusCode);
}

public sealed record ApiSuccessResponse(bool Success = true);

public sealed record ApiSuccessResponse<T>(T Data)
{
    public bool Success { get; init; } = true;
}

public sealed record ApiFailureResponse(string Error)
{
    public bool Success { get; init; }
}
