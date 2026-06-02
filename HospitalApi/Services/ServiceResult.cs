namespace HospitalApi.Services;

public class ServiceResult<T>
{
    public bool IsSuccess { get; set; }
    public int StatusCode { get; set; }
    public string? ErrorMessage { get; set; }
    public T? Data { get; set; }

    public static ServiceResult<T> Success(T data, int statusCode = StatusCodes.Status200OK)
    {
        return new ServiceResult<T>
        {
            IsSuccess = true,
            StatusCode = statusCode,
            Data = data
        };
    }

    public static ServiceResult<T> Failure(int statusCode, string errorMessage)
    {
        return new ServiceResult<T>
        {
            IsSuccess = false,
            StatusCode = statusCode,
            ErrorMessage = errorMessage
        };
    }
}