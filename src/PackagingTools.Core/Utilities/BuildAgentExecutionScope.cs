using System.Threading;
using PackagingTools.Core.Abstractions;

namespace PackagingTools.Core.Utilities;

/// <summary>
/// Ambient context helper that exposes the currently allocated build agent to downstream services.
/// </summary>
public static class BuildAgentExecutionScope
{
    private static readonly AsyncLocal<Scope?> CurrentScope = new();

    /// <summary>
    /// Gets the build agent associated with the current asynchronous flow, if any.
    /// </summary>
    public static IBuildAgentHandle? Current => CurrentScope.Value?.Handle;

    /// <summary>
    /// Pushes the provided build agent into the current execution context.
    /// </summary>
    public static IDisposable Push(IBuildAgentHandle handle)
    {
        var scope = new Scope(handle, CurrentScope.Value);
        CurrentScope.Value = scope;
        return scope;
    }

    private sealed class Scope : IDisposable
    {
        private readonly Scope? _parent;
        private bool _disposed;

        public Scope(IBuildAgentHandle handle, Scope? parent)
        {
            Handle = handle;
            _parent = parent;
        }

        public IBuildAgentHandle Handle { get; }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (CurrentScope.Value == this)
            {
                CurrentScope.Value = _parent;
            }
        }
    }
}
