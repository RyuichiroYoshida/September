using System;

namespace September.Editor.AssetImporter
{
    public class Result<T>
    {
        public bool IsSuccess { get; private set; }
        public T Value { get; private set; }
        public string Error { get; private set; }
        public Exception Exception { get; private set; }

        private Result(bool isSuccess, T value, string error, Exception exception = null)
        {
            IsSuccess = isSuccess;
            Value = value;
            Error = error;
            Exception = exception;
        }

        public static Result<T> Success(T value)
        {
            return new Result<T>(true, value, null);
        }

        public static Result<T> Failure(string error, Exception exception = null)
        {
            return new Result<T>(false, default(T), error, exception);
        }

        public static Result<T> Failure(Exception exception)
        {
            return new Result<T>(false, default(T), exception.Message, exception);
        }

        public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<string, Exception, TResult> onFailure)
        {
            return IsSuccess ? onSuccess(Value) : onFailure(Error, Exception);
        }

        public void Match(Action<T> onSuccess, Action<string, Exception> onFailure)
        {
            if (IsSuccess)
                onSuccess(Value);
            else
                onFailure(Error, Exception);
        }
    }

    public class Result
    {
        public bool IsSuccess { get; private set; }
        public string Error { get; private set; }
        public Exception Exception { get; private set; }

        private Result(bool isSuccess, string error, Exception exception = null)
        {
            IsSuccess = isSuccess;
            Error = error;
            Exception = exception;
        }

        public static Result Success()
        {
            return new Result(true, null);
        }

        public static Result Failure(string error, Exception exception = null)
        {
            return new Result(false, error, exception);
        }

        public static Result Failure(Exception exception)
        {
            return new Result(false, exception.Message, exception);
        }

        public void Match(Action onSuccess, Action<string, Exception> onFailure)
        {
            if (IsSuccess)
                onSuccess();
            else
                onFailure(Error, Exception);
        }
    }
}