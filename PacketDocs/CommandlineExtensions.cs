using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;
using System.Threading.Tasks;

namespace PacketDocs;

internal static class CommandlineExtensions
{
    public static Command WithHandler(this Command command, Delegate handler)
    {
        command.SetHandler(context =>
        {
            object?[] args = GetHandlerArgs(command, context).ToArray();
            object? result = handler.DynamicInvoke(args);
            if (result is Task task)
                return task;
            else
                return Task.CompletedTask;
        });

        return command;
    }

    private static IEnumerable<object?> GetHandlerArgs(Command command, InvocationContext context) =>
        command.Parents.OfType<Command>().SelectMany(x => GetHandlerArgs(x, context))
        .Concat(command.Options
            .Where(x => x.Name != "version" && x.Name != "help")
            .Select(context.BindingContext.ParseResult.GetValueForOption)
        )
        .Concat(command.Arguments.Select(context.BindingContext.ParseResult.GetValueForArgument));

    public static Command WithGlobalOption(this Command command, Option option)
    {
        command.AddGlobalOption(option);
        return command;
    }

    public static Option<T> Required<T>(this Option<T> option)
    {
        option.IsRequired = true;
        return option;
    }

    public static Argument<T> Arity<T>(this Argument<T> argument, ArgumentArity arity)
    {
        argument.Arity = arity;
        return argument;
    }
}
