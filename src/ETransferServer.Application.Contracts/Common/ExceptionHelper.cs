using System;
using System.Threading.Tasks;
using AElf.ExceptionHandler;

namespace ETransferServer.Common;

public class ExceptionHelper
{
    public static async Task<FlowBehavior> HandleException(Exception ex, object obj)
    {
        Console.WriteLine($"Handled exception: {ex.Message}");
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = obj
        };
    }
}