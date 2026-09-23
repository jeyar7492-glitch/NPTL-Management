using System.Net;

namespace NPTELManagement.Desktop.Services;

public class ApiException : Exception
{
    public HttpStatusCode? StatusCode { get; }
    public List<string> Errors { get; }

    public ApiException(string message, HttpStatusCode? statusCode = null, List<string>? errors = null) 
        : base(message)
    {
        StatusCode = statusCode;
        Errors = errors ?? new List<string>();
    }
}

public class AccessDeniedException : ApiException
{
    public AccessDeniedException(string message = "Access denied. You do not have permission to perform this action.") 
        : base(message, HttpStatusCode.Forbidden) { }
}

public class UnauthorizedException : ApiException
{
    public UnauthorizedException(string message = "Your session has expired or is invalid. Please log in again.") 
        : base(message, HttpStatusCode.Unauthorized) { }
}

public class NotFoundException : ApiException
{
    public NotFoundException(string message = "The requested resource was not found.") 
        : base(message, HttpStatusCode.NotFound) { }
}

public class ServerErrorException : ApiException
{
    public ServerErrorException(string message = "A server error occurred. Please try again later.") 
        : base(message, HttpStatusCode.InternalServerError) { }
}

public class NetworkUnavailableException : ApiException
{
    public NetworkUnavailableException(string message = "Unable to connect to the NPTEL Management server. Please check your network connection.") 
        : base(message) { }
}
