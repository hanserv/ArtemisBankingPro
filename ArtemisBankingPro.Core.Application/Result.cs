namespace ArtemisBankingPro.Core.Application
{
    public class Result
    {
        public bool IsSuccess { get; set; }
        public string? Error { get; set; }
        public string? Message { get; set; }

        protected Result(bool isSuccess, string error, string message = "")
        {
            IsSuccess = isSuccess;
            Error = error;
            Message = message;
        }

        public static Result Success() => new Result(true, "");
        public static Result Success(string message) => new Result(true, "", message);
        public static Result Failure(string error) => new Result(false, error, "");
    }

    public class Result<T> : Result
    {
        public T? Value { get; set; }

        protected Result(T? value, bool isSuccess, string error, string message = "") : base(isSuccess, error, message)
        {
            Value = value;
        }

        public static Result<T> Success(T value) => new Result<T>(value, true, "");
        public static Result<T> Success(T value, string message) => new Result<T>(value, true, "", message);
        public new static Result<T> Failure(string error) => new Result<T>(default, false, error);
    }
}
