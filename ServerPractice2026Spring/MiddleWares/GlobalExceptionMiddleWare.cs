using System.ComponentModel.DataAnnotations;
using System.Text;

namespace ServerPractice2026Spring.MiddleWares;

/// <summary>
/// Класс для отловли ошибок происходящих в запросах
/// </summary>
/// <param name="request">Делегат запроса</param>
/// <param name="environment">Окружение запроса</param>
/// <param name="logger">Логгер для логгирования в консоль</param>
public class GlobalExceptionMiddleWare(
    RequestDelegate request,
    IWebHostEnvironment environment,
    ILogger<GlobalExceptionMiddleWare> logger) : IExceptionMiddleWare
{
    public static int GetErrorCode(Exception exception)
    {
        return exception switch
        {
            UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
            KeyNotFoundException => StatusCodes.Status404NotFound,
            ArgumentException or ValidationException => StatusCodes.Status400BadRequest,
            CustomException ex => ex.ErrorCode,
            _ => StatusCodes.Status500InternalServerError
        };
    }

    public static string GetErrorMessage(Exception exception)
    {
        return exception switch
        {
            UnauthorizedAccessException => "Доступ запрещён: требуется аутентификация.",
            KeyNotFoundException => "Запрашиваемый ресурс не найден.",
            ArgumentException => "Некорректные входные данные.",
            ValidationException => "Ошибка валидации данных.",
            CustomException ex => UnwrapException(ex),
            _ => "Произошла внутренняя ошибка сервера."
        };
    }

    public static async Task HandleExceptionAsync(HttpContext httpContext, Exception exception, bool isDevelopment)
    {
        var errorCode = GetErrorCode(exception);
        var errorMessage = GetErrorMessage(exception);
        //Задаём ответу статус код
        httpContext.Response.StatusCode = errorCode;

        //Создаём анонимный класс для превращения его в json для тела ответа
        var response = new
        {
            errorCode,
            errorMessage,
            details = isDevelopment ? exception.Message : null
        };

        //Записываем ошибку в тело ответа
        await httpContext.Response.WriteAsJsonAsync(response);
    }

    public async Task InvokeAsync(HttpContext httpContext)
    {
        try
        {
            // Пробуем запустить запрос в контексте
            await request.Invoke(httpContext);
        }
        //Отлавливаем возможную ошибку
        catch (Exception ex)
        {
            var message = ex switch
            {
                UnauthorizedAccessException => "Произошёл запрос от неаутентифицированного пользователя",
                KeyNotFoundException => "Пользователь не нашёл запрашиваемый ресурс",
                ArgumentException => "Пользователь ввёл некорректные входные данные",
                ValidationException => "Данные пользователя не прошли валидацию",
                CustomException e => e.ErrorMessage, // Пока так 
                _ => $"Произошла непредвиденная ошибка, трассировка: {ex}"
            };

            var severity = GetSeverityOfError(ex);
            //Логгируем ошибку в консоль
            logger.Log(severity,
                "MiddleWare поймал ошибку в выполнении запроса пользователя {0} в методе {1} {2}, ошибка: {3}",
                httpContext.Connection.RemoteIpAddress, httpContext.Request.Method, httpContext.Request.Path,
                message);
            //Запускаем метод для отправки ошибки клиенту
            await HandleExceptionAsync(httpContext, ex, environment.IsDevelopment());
        }
    }

    private static LogLevel GetSeverityOfError(Exception exception)
    {
        return exception switch
        {
            UnauthorizedAccessException
                or KeyNotFoundException
                or ArgumentException
                or ValidationException => LogLevel.Information,
            CustomException ex => ex.ErrorCode >= 500 ? LogLevel.Error : LogLevel.Information,
            _ => LogLevel.Error
        };
    }

    private static string UnwrapException(Exception e)
    {
        StringBuilder sb = new();
        sb.Append(e.Message);
        while (true)
        {
            if (e.InnerException is null)
            {
                break;
            }
            sb.AppendLine(e.InnerException.Message);
        }

        return sb.ToString();
    }
}