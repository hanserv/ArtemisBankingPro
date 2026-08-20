namespace ArtemisBankingPro.Core.Application
{
    public class Result
    {
        public bool IsSuccess { get; set; }
        public string? Error { get; set; }
        public string? Message { get; set; }
        public int StatusCode { get; set; }

        protected Result(bool isSuccess, string error, string message = "", int statusCode = 400)
        {
            IsSuccess = isSuccess;
            Error = error;
            Message = message;
            StatusCode = statusCode;
        }

        public static Result Success() => new Result(true, "");
        public static Result Success(string message) => new Result(true, "", message);
        public static Result Failure(string error) => new Result(false, error, "");
        public static Result Failure(string error, int statusCode) => new Result(false, error, "", statusCode);
    }

    public class Result<T> : Result
    {
        public T? Value { get; set; }

        protected Result(T? value, bool isSuccess, string error, string message = "", int statusCode = 400)
            : base(isSuccess, error, message, statusCode)
        {
            Value = value;
        }

        public static Result<T> Success(T value) => new Result<T>(value, true, "");
        public static Result<T> Success(T value, string message) => new Result<T>(value, true, "", message);
        public new static Result<T> Failure(string error) => new Result<T>(default, false, error);
        public new static Result<T> Failure(string error, int statusCode) => new Result<T>(default, false, error, "", statusCode);
    }
}
