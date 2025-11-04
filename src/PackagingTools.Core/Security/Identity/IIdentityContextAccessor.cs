using System.Threading;

namespace PackagingTools.Core.Security.Identity;

/// <summary>
/// Provides access to the identity associated with the current packaging invocation.
/// </summary>
public interface IIdentityContextAccessor
{
    IdentityResult? Identity { get; }

    void SetIdentity(IdentityResult identity);

    void Clear();
}

internal sealed class IdentityContextAccessor : IIdentityContextAccessor
{
    private IdentityResult? _identity;

    private readonly object _gate = new();

    public IdentityResult? Identity
    {
        get
        {
            lock (_gate)
            {
                return _identity;
            }
        }
    }

    public void SetIdentity(IdentityResult identity)
    {
        lock (_gate)
        {
            _identity = identity;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _identity = null;
        }
    }
}
