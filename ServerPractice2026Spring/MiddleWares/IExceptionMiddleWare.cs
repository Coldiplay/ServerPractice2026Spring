namespace ServerPractice2026Spring.MiddleWares;

/// <summary>
/// Интерфейс обработчика ошибок
/// </summary>
public interface IExceptionMiddleWare
{
    /// <summary>
    /// Обёртка для возвращения http кода ошибки
    /// </summary>
    /// <param name="exception">Ошибка, тип которой трактуем в http статус код</param>
    static abstract int GetErrorCode(Exception exception);

    /// <summary>
    /// Обёртка для текста ошибки
    /// </summary>
    /// <param name="exception">Ошибка, тип которой трактуем в сообщение</param>
    static abstract string GetErrorMessage(Exception exception);

    /// <summary>
    /// Метод для потенциального отлова ошибки
    /// </summary>
    /// <param name="httpContext">Контекст запроса</param>
    Task InvokeAsync(HttpContext httpContext);

    /// <summary>
    /// Метод вызываемый при ловли ошибки
    /// </summary>
    /// <param name="httpContext">Контекст запроса</param>
    /// <param name="exception">Отловленная ошибка</param>
    /// <param name="isDevelopment">Находится ли окружение в среде разработки</param>
    static abstract Task HandleExceptionAsync(HttpContext httpContext, Exception exception, bool isDevelopment);
}